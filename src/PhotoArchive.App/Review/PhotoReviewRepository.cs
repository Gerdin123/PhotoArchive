using Microsoft.EntityFrameworkCore;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.App.Review;

public sealed class PhotoReviewRepository
{
    private readonly string databasePath;

    public PhotoReviewRepository(string databasePath)
    {
        this.databasePath = databasePath;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagOption>> GetTagOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.Tags
            .OrderBy(tag => tag.Type)
            .ThenBy(tag => tag.Name)
            .Select(tag => new TagOption(tag.Id, tag.Name, tag.Type))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewPhoto>> GetPhotosAsync(
        ReviewFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await GetPhotoPageAsync(filter, pageNumber: 1, pageSize: 1000, cancellationToken);
        return page.Photos;
    }

    public async Task<ReviewPhotoPage> GetPhotoPageAsync(
        ReviewFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var query = from file in dbContext.ArchiveFiles
                    join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId into metadataJoin
                    from metadata in metadataJoin.DefaultIfEmpty()
                    select new { file, metadata };

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var text = filter.SearchText.Trim();
            query = query.Where(row =>
                row.file.OriginalFileName.Contains(text)
                || row.file.OriginalPath.Contains(text)
                || (row.file.CurrentPath != null && row.file.CurrentPath.Contains(text)));
        }

        if (filter.Status is not null)
        {
            query = query.Where(row => row.file.Status == filter.Status);
        }

        if (filter.DuplicatesOnly)
        {
            query = query.Where(row => row.file.Status == ArchiveFileStatus.Duplicate || row.file.MediaKind == MediaKind.Duplicate);
        }

        if (filter.UncertainOrUnprocessedOnly)
        {
            query = query.Where(row =>
                row.file.Status != ArchiveFileStatus.Processed
                || row.metadata == null
                || row.metadata.DateConfidence == DateConfidence.Low
                || row.metadata.DateConfidence == DateConfidence.Unknown);
        }

        if (filter.TagId is not null)
        {
            query = query.Where(row => dbContext.PhotoTags.Any(photoTag =>
                photoTag.ArchiveFileId == row.file.Id && photoTag.TagId == filter.TagId));
        }

        var allRows = (await query
            .OrderBy(row => row.file.OriginalPath)
            .Take(5000)
            .ToListAsync(cancellationToken))
            .Select(row => new PhotoRow(row.file, row.metadata))
            .Where(row => filter.From is null || row.metadata?.InferredTakenDate >= filter.From)
            .Where(row => filter.To is null || row.metadata?.InferredTakenDate <= filter.To)
            .ToList();

        var sortedRows = SortRows(allRows, filter.SortMode).ToList();
        var totalCount = sortedRows.Count;
        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var safePageNumber = Math.Max(1, pageNumber);
        var rows = sortedRows
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        var fileIds = rows.Select(row => row.file.Id).ToArray();
        var tagsByFileId = await GetTagNamesByFileIdAsync(dbContext, fileIds, cancellationToken);

        var photos = rows.Select(row => ToReviewPhoto(
                row.file,
                row.metadata,
                tagsByFileId.TryGetValue(row.file.Id, out var tags) ? tags : string.Empty))
            .ToList();

        return new ReviewPhotoPage(photos, safePageNumber, safePageSize, totalCount);
    }

    public async Task<ReviewPhotoDetails?> GetDetailsAsync(Guid archiveFileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var row = await (from file in dbContext.ArchiveFiles
                         join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId into metadataJoin
                         from metadata in metadataJoin.DefaultIfEmpty()
                         where file.Id == archiveFileId
                         select new { file, metadata })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var tagRows = await (from photoTag in dbContext.PhotoTags
                             join tag in dbContext.Tags on photoTag.TagId equals tag.Id
                             where photoTag.ArchiveFileId == archiveFileId
                             orderby tag.Type, tag.Name
                             select tag)
            .ToListAsync(cancellationToken);

        var tagsText = tagRows.Count == 0
            ? string.Empty
            : string.Join(", ", tagRows.Select(tag => tag.Name));

        var photo = ToReviewPhoto(row.file, row.metadata, tagsText);
        var nearby = await GetNearbyPhotosAsync(dbContext, photo, cancellationToken);
        var related = await GetRelatedPhotosAsync(dbContext, row.file, row.metadata, tagRows, cancellationToken);
        var duplicateGroup = await GetDuplicateGroupAsync(dbContext, row.file, cancellationToken);

        return new ReviewPhotoDetails(photo, row.metadata, tagRows, nearby, related, duplicateGroup);
    }

    public async Task CorrectTakenDateAsync(
        Guid archiveFileId,
        DateTimeOffset newDate,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var metadata = await dbContext.PhotoMetadata.FindAsync([archiveFileId], cancellationToken);
        if (metadata is null)
        {
            metadata = new PhotoMetadata { ArchiveFileId = archiveFileId };
            dbContext.PhotoMetadata.Add(metadata);
        }

        var oldValue = metadata.InferredTakenDate?.ToString("O");
        metadata.InferredTakenDate = newDate;
        metadata.DateConfidence = DateConfidence.High;

        dbContext.ManualCorrections.Add(new ManualCorrection
        {
            ArchiveFileId = archiveFileId,
            FieldName = nameof(PhotoMetadata.InferredTakenDate),
            OldValue = oldValue,
            NewValue = newDate.ToString("O"),
            Reason = string.IsNullOrWhiteSpace(reason) ? "Manual review correction" : reason
        });

        await AddOperationLogAsync(dbContext, archiveFileId, "DateCorrection", "Recorded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Tag> AddTagAsync(
        Guid archiveFileId,
        string tagName,
        TagType tagType,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var normalized = tagName.Trim();
        var tag = await dbContext.Tags
            .SingleOrDefaultAsync(existing => existing.Name == normalized && existing.Type == tagType, cancellationToken);

        if (tag is null)
        {
            tag = new Tag { Name = normalized, Type = tagType };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var exists = await dbContext.PhotoTags.AnyAsync(
            photoTag => photoTag.ArchiveFileId == archiveFileId && photoTag.TagId == tag.Id,
            cancellationToken);

        if (!exists)
        {
            dbContext.PhotoTags.Add(new PhotoTag { ArchiveFileId = archiveFileId, TagId = tag.Id });
            await AddOperationLogAsync(dbContext, archiveFileId, "TagAdded", "Recorded", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return tag;
    }

    public async Task RemoveTagAsync(Guid archiveFileId, Guid tagId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var photoTag = await dbContext.PhotoTags.FindAsync([archiveFileId, tagId], cancellationToken);
        if (photoTag is null)
        {
            return;
        }

        dbContext.PhotoTags.Remove(photoTag);
        await AddOperationLogAsync(dbContext, archiveFileId, "TagRemoved", "Recorded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDuplicateAsync(
        Guid duplicateFileId,
        Guid canonicalFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var duplicate = await dbContext.ArchiveFiles.FindAsync([duplicateFileId], cancellationToken)
            ?? throw new InvalidOperationException("Duplicate file was not found.");
        var canonical = await dbContext.ArchiveFiles.FindAsync([canonicalFileId], cancellationToken)
            ?? throw new InvalidOperationException("Canonical file was not found.");

        duplicate.Status = ArchiveFileStatus.Duplicate;
        duplicate.MediaKind = MediaKind.Duplicate;
        duplicate.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var hash = duplicate.Sha256Hash ?? canonical.Sha256Hash ?? duplicate.Id.ToString("N");
        var group = await dbContext.DuplicateGroups.SingleOrDefaultAsync(existing => existing.Hash == hash, cancellationToken);
        if (group is null)
        {
            dbContext.DuplicateGroups.Add(new DuplicateGroup
            {
                Hash = hash,
                CanonicalFileId = canonical.Id
            });
        }
        else
        {
            group.CanonicalFileId = canonical.Id;
        }

        await AddOperationLogAsync(dbContext, duplicateFileId, "DuplicateMarked", "Recorded", cancellationToken, canonical.CurrentPath ?? canonical.OriginalPath);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task HideAsync(Guid archiveFileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var file = await dbContext.ArchiveFiles.FindAsync([archiveFileId], cancellationToken);
        if (file is null)
        {
            return;
        }

        file.Status = ArchiveFileStatus.Deleted;
        file.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await AddOperationLogAsync(dbContext, archiveFileId, "Hide", "Recorded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private PhotoArchiveDbContext CreateDbContext()
    {
        return PhotoArchiveDbContextFactory.Create(databasePath);
    }

    private static async Task<Dictionary<Guid, string>> GetTagNamesByFileIdAsync(
        PhotoArchiveDbContext dbContext,
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken cancellationToken)
    {
        var rows = await (from photoTag in dbContext.PhotoTags
                          join tag in dbContext.Tags on photoTag.TagId equals tag.Id
                          where fileIds.Contains(photoTag.ArchiveFileId)
                          orderby tag.Type, tag.Name
                          select new { photoTag.ArchiveFileId, tag.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ArchiveFileId)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(row => row.Name)));
    }

    private static async Task<IReadOnlyList<ReviewPhoto>> GetNearbyPhotosAsync(
        PhotoArchiveDbContext dbContext,
        ReviewPhoto photo,
        CancellationToken cancellationToken)
    {
        if (photo.InferredTakenDate is null)
        {
            return [];
        }

        var start = photo.InferredTakenDate.Value.AddDays(-1);
        var end = photo.InferredTakenDate.Value.AddDays(1);
        var rows = (await (from file in dbContext.ArchiveFiles
                          join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId
                          where file.Id != photo.Id
                          select new { file, metadata })
            .ToListAsync(cancellationToken))
            .Where(row => row.metadata.InferredTakenDate >= start && row.metadata.InferredTakenDate <= end)
            .OrderBy(row => row.metadata.InferredTakenDate)
            .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return rows.Select(row => ToReviewPhoto(row.file, row.metadata, string.Empty)).ToList();
    }

    private static async Task<IReadOnlyList<ReviewPhoto>> GetDuplicateGroupAsync(
        PhotoArchiveDbContext dbContext,
        ArchiveFile file,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file.Sha256Hash))
        {
            return [];
        }

        var rows = await (from candidate in dbContext.ArchiveFiles
                          join metadata in dbContext.PhotoMetadata on candidate.Id equals metadata.ArchiveFileId into metadataJoin
                          from metadata in metadataJoin.DefaultIfEmpty()
                          where candidate.Sha256Hash == file.Sha256Hash && candidate.Id != file.Id
                          orderby candidate.OriginalPath
                          select new { candidate, metadata })
            .ToListAsync(cancellationToken);

        return rows.Select(row => ToReviewPhoto(row.candidate, row.metadata, string.Empty)).ToList();
    }

    private static async Task<IReadOnlyList<RelatedReviewPhoto>> GetRelatedPhotosAsync(
        PhotoArchiveDbContext dbContext,
        ArchiveFile file,
        PhotoMetadata? metadata,
        IReadOnlyList<Tag> tags,
        CancellationToken cancellationToken)
    {
        var sourceDirectory = Path.GetDirectoryName(file.OriginalPath);
        var tagIds = tags.Select(tag => tag.Id).ToHashSet();
        var sameDayStart = metadata?.InferredTakenDate?.Date;
        var sameDayEnd = sameDayStart?.AddDays(1).AddTicks(-1);

        var rows = await (from candidate in dbContext.ArchiveFiles
                          join candidateMetadata in dbContext.PhotoMetadata on candidate.Id equals candidateMetadata.ArchiveFileId into metadataJoin
                          from candidateMetadata in metadataJoin.DefaultIfEmpty()
                          where candidate.Id != file.Id
                          select new { candidate, candidateMetadata })
            .Take(2000)
            .ToListAsync(cancellationToken);

        var candidateIds = rows.Select(row => row.candidate.Id).ToArray();
        var candidateTagRows = await dbContext.PhotoTags
            .Where(photoTag => candidateIds.Contains(photoTag.ArchiveFileId))
            .ToListAsync(cancellationToken);
        var candidateTagsByFileId = candidateTagRows
            .GroupBy(photoTag => photoTag.ArchiveFileId)
            .ToDictionary(group => group.Key, group => group.Select(photoTag => photoTag.TagId).ToHashSet());

        var related = new List<RelatedReviewPhoto>();
        foreach (var row in rows)
        {
            var reasons = new List<string>();
            var score = 0;

            if (sameDayStart is not null
                && row.candidateMetadata?.InferredTakenDate >= sameDayStart
                && row.candidateMetadata?.InferredTakenDate <= sameDayEnd)
            {
                reasons.Add("same day");
                score += 30;
            }

            if (!string.IsNullOrWhiteSpace(sourceDirectory)
                && string.Equals(Path.GetDirectoryName(row.candidate.OriginalPath), sourceDirectory, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("same source folder");
                score += 20;
            }

            if (!string.IsNullOrWhiteSpace(metadata?.CameraModel)
                && string.Equals(metadata.CameraModel, row.candidateMetadata?.CameraModel, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("same camera");
                score += 15;
            }

            if (metadata?.Width is not null
                && metadata.Height is not null
                && metadata.Width == row.candidateMetadata?.Width
                && metadata.Height == row.candidateMetadata?.Height)
            {
                reasons.Add("same dimensions");
                score += 10;
            }

            if (!string.IsNullOrWhiteSpace(file.Sha256Hash)
                && string.Equals(file.Sha256Hash, row.candidate.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("same hash");
                score += 50;
            }

            if (tagIds.Count > 0
                && candidateTagsByFileId.TryGetValue(row.candidate.Id, out var candidateTagIds)
                && tagIds.Overlaps(candidateTagIds))
            {
                reasons.Add("shared tag");
                score += 20;
            }

            if (score > 0)
            {
                related.Add(new RelatedReviewPhoto(
                    ToReviewPhoto(row.candidate, row.candidateMetadata, string.Empty),
                    string.Join(", ", reasons),
                    score));
            }
        }

        return related
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Photo.InferredTakenDate is null)
            .ThenBy(item => item.Photo.InferredTakenDate)
            .ThenBy(item => item.Photo.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .Take(30)
            .ToList();
    }

    private static ReviewPhoto ToReviewPhoto(ArchiveFile file, PhotoMetadata? metadata, string tags)
    {
        return new ReviewPhoto(
            Id: file.Id,
            OriginalPath: file.OriginalPath,
            CurrentPath: file.CurrentPath,
            OriginalFileName: file.OriginalFileName,
            MediaKind: file.MediaKind,
            Status: file.Status,
            Sha256Hash: file.Sha256Hash,
            InferredTakenDate: metadata?.InferredTakenDate,
            DateConfidence: metadata?.DateConfidence ?? DateConfidence.Unknown,
            Tags: tags);
    }

    private static IOrderedEnumerable<PhotoRow> SortRows(
        IReadOnlyList<PhotoRow> rows,
        ReviewSortMode sortMode)
    {
        return sortMode switch
        {
            ReviewSortMode.DateDescending => rows
                .OrderBy(row => row.metadata?.InferredTakenDate is null)
                .ThenByDescending(row => row.metadata?.InferredTakenDate)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase),
            ReviewSortMode.FileName => rows
                .OrderBy(row => row.file.OriginalFileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase),
            ReviewSortMode.Status => rows
                .OrderBy(row => row.file.Status)
                .ThenBy(row => row.metadata?.InferredTakenDate is null)
                .ThenBy(row => row.metadata?.InferredTakenDate),
            ReviewSortMode.DateConfidence => rows
                .OrderBy(row => row.metadata?.DateConfidence ?? DateConfidence.Unknown)
                .ThenBy(row => row.metadata?.InferredTakenDate is null)
                .ThenBy(row => row.metadata?.InferredTakenDate),
            _ => rows
                .OrderBy(row => row.metadata?.InferredTakenDate is null)
                .ThenBy(row => row.metadata?.InferredTakenDate)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed record PhotoRow(ArchiveFile file, PhotoMetadata? metadata);

    private static async Task AddOperationLogAsync(
        PhotoArchiveDbContext dbContext,
        Guid archiveFileId,
        string operationType,
        string result,
        CancellationToken cancellationToken,
        string? destinationPath = null)
    {
        var file = await dbContext.ArchiveFiles.FindAsync([archiveFileId], cancellationToken);
        if (file is null)
        {
            return;
        }

        dbContext.OperationLogs.Add(new OperationLog
        {
            OperationType = operationType,
            SourcePath = file.OriginalPath,
            DestinationPath = destinationPath ?? file.CurrentPath,
            Result = result
        });
    }
}
