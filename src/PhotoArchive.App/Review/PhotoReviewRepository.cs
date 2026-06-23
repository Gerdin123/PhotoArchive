using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Diagnostics;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure.Metadata;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.App.Review;

public sealed class PhotoReviewRepository
{
    private readonly string databasePath;
    private readonly DecadeBucketPolicy decadeBucketPolicy = DecadeBucketPolicy.Default;
    private readonly IApplicationLogger logger;

    public PhotoReviewRepository(string databasePath, IApplicationLogger? logger = null)
    {
        this.databasePath = databasePath;
        this.logger = logger ?? AppLog.Current;
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

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var dates = await dbContext.PhotoMetadata
            .Where(metadata => metadata.InferredTakenDate != null)
            .Select(metadata => metadata.InferredTakenDate)
            .ToListAsync(cancellationToken);

        return dates
            .Where(date => date is not null)
            .Select(date => date!.Value.Year)
            .Distinct()
            .Order()
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetAvailableDecadesAsync(CancellationToken cancellationToken = default)
    {
        var years = await GetAvailableYearsAsync(cancellationToken);
        return years
            .Select(year => year / 10 * 10)
            .Distinct()
            .Order()
            .ToList();
    }

    public async Task<IReadOnlyList<ReviewPhoto>> GetPhotosAsync(
        ReviewFilter filter,
        CancellationToken cancellationToken = default)
    {
        var photos = new List<ReviewPhoto>();
        var pageNumber = 1;
        ReviewPhotoPage page;
        do
        {
            page = await GetPhotoPageAsync(filter, pageNumber, pageSize: 200, cancellationToken);
            photos.AddRange(page.Photos);
            pageNumber++;
        }
        while (page.PageNumber < page.TotalPages);

        return photos;
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
                || (row.file.CurrentPath != null && row.file.CurrentPath.Contains(text))
                || (row.metadata != null && row.metadata.Title != null && row.metadata.Title.Contains(text)));
        }

        if (filter.Status is not null)
        {
            query = query.Where(row => row.file.Status == filter.Status);
        }

        if (filter.DuplicatesOnly)
        {
            query = query.Where(row => row.file.Status == ArchiveFileStatus.Duplicate || row.file.MediaKind == MediaKind.Duplicate);
        }
        else if (!filter.IncludeDuplicates)
        {
            query = query.Where(row => row.file.Status != ArchiveFileStatus.Duplicate && row.file.MediaKind != MediaKind.Duplicate);
        }

        if (!filter.IncludeUnsupported && !filter.DuplicatesOnly)
        {
            query = query.Where(row =>
                row.file.MediaKind == MediaKind.SupportedImage
                || (filter.IncludeDuplicates
                    && (row.file.Status == ArchiveFileStatus.Duplicate || row.file.MediaKind == MediaKind.Duplicate)));
        }

        if (!filter.IncludeDeleted)
        {
            query = query.Where(row => row.file.Status != ArchiveFileStatus.Deleted);
        }

        if (filter.UncertainOrUnprocessedOnly)
        {
            query = query.Where(row =>
                row.file.Status != ArchiveFileStatus.Processed
                || row.metadata == null
                || row.metadata.DateConfidence == DateConfidence.Low
                || row.metadata.DateConfidence == DateConfidence.Unknown);
        }

        var selectedTagIds = (filter.TagIds ?? [])
            .Concat(filter.TagId is null ? [] : [filter.TagId.Value])
            .Distinct()
            .ToArray();
        foreach (var tagId in selectedTagIds)
        {
            var requiredTagId = tagId;
            query = query.Where(row => dbContext.PhotoTags.Any(photoTag =>
                photoTag.ArchiveFileId == row.file.Id && photoTag.TagId == requiredTagId));
        }

        if (filter.NoTagsOnly)
        {
            query = query.Where(row => !dbContext.PhotoTags.Any(photoTag => photoTag.ArchiveFileId == row.file.Id));
        }

        var safePageSize = Math.Clamp(pageSize, 1, 200);
        var safePageNumber = Math.Max(1, pageNumber);
        var summary = await GetPageSummaryAsync(dbContext, cancellationToken);
        var allRows = (await query
            .OrderBy(row => row.file.OriginalPath)
            .ToListAsync(cancellationToken))
            .Select(row => new PhotoRow(row.file, row.metadata))
            .Where(row => filter.From is null || row.metadata?.InferredTakenDate >= filter.From)
            .Where(row => filter.To is null || row.metadata?.InferredTakenDate <= filter.To)
            .ToList();

        var sortedRows = SortRows(allRows, filter.SortMode).ToList();
        var totalCount = sortedRows.Count;
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

        return new ReviewPhotoPage(photos, safePageNumber, safePageSize, totalCount)
        {
            Summary = summary
        };
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
        var oldDate = metadata.InferredTakenDate;
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
        await ResequenceAffectedDaysAsync(dbContext, oldDate, newDate, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.Info(nameof(PhotoReviewRepository), $"Corrected taken date for file '{archiveFileId}' from '{oldValue ?? "(none)"}' to '{newDate:O}'.");
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
            logger.Info(nameof(PhotoReviewRepository), $"Added tag '{normalized}' ({tagType}) to file '{archiveFileId}'.");
        }

        return tag;
    }

    public async Task UpdateTitleAsync(Guid archiveFileId, string? title, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var metadata = await dbContext.PhotoMetadata.FindAsync([archiveFileId], cancellationToken);
        if (metadata is null)
        {
            metadata = new PhotoMetadata { ArchiveFileId = archiveFileId };
            dbContext.PhotoMetadata.Add(metadata);
        }

        var oldValue = metadata.Title;
        metadata.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        dbContext.ManualCorrections.Add(new ManualCorrection
        {
            ArchiveFileId = archiveFileId,
            FieldName = nameof(PhotoMetadata.Title),
            OldValue = oldValue,
            NewValue = metadata.Title ?? string.Empty,
            Reason = "Manual title edit"
        });
        await AddOperationLogAsync(dbContext, archiveFileId, "TitleUpdated", "Recorded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.Info(nameof(PhotoReviewRepository), $"Updated title for file '{archiveFileId}'.");
    }

    public async Task WriteImageMetadataAsync(Guid archiveFileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var row = await (from file in dbContext.ArchiveFiles
                         join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId into metadataJoin
                         from metadata in metadataJoin.DefaultIfEmpty()
                         where file.Id == archiveFileId
                         select new { file, metadata })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw LogAndCreateInvalidOperation("Image was not found.", archiveFileId);
        var targetPath = row.file.CurrentPath ?? row.file.OriginalPath;
        var tags = await (from photoTag in dbContext.PhotoTags
                          join tag in dbContext.Tags on photoTag.TagId equals tag.Id
                          where photoTag.ArchiveFileId == archiveFileId
                          orderby tag.Type, tag.Name
                          select tag.Name)
            .ToListAsync(cancellationToken);

        await new EmbeddedXmpMetadataWriter().WriteAsync(
            new MetadataWriteRequest(
                targetPath,
                row.metadata?.InferredTakenDate,
                PreferSidecar: false,
                Title: row.metadata?.Title,
                Tags: tags),
            cancellationToken);
        dbContext.OperationLogs.Add(new OperationLog
        {
            OperationType = "EmbeddedMetadataWrite",
            SourcePath = targetPath,
            DestinationPath = targetPath,
            Result = "Written"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.Info(nameof(PhotoReviewRepository), $"Wrote embedded metadata for file '{archiveFileId}'.");
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
        logger.Info(nameof(PhotoReviewRepository), $"Removed tag '{tagId}' from file '{archiveFileId}'.");
    }

    public async Task MarkDuplicateAsync(
        Guid duplicateFileId,
        Guid canonicalFileId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var duplicate = await dbContext.ArchiveFiles.FindAsync([duplicateFileId], cancellationToken)
            ?? throw LogAndCreateInvalidOperation("Duplicate file was not found.", duplicateFileId);
        var canonical = await dbContext.ArchiveFiles.FindAsync([canonicalFileId], cancellationToken)
            ?? throw LogAndCreateInvalidOperation("Canonical file was not found.", canonicalFileId);

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
        logger.Info(nameof(PhotoReviewRepository), $"Marked file '{duplicateFileId}' as duplicate of '{canonicalFileId}'.");
    }

    public async Task HideAsync(Guid archiveFileId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();
        var file = await dbContext.ArchiveFiles.FindAsync([archiveFileId], cancellationToken);
        if (file is null)
        {
            logger.Warning(nameof(PhotoReviewRepository), $"Hide ignored because file '{archiveFileId}' was not found.");
            return;
        }

        file.Status = ArchiveFileStatus.Deleted;
        file.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await AddOperationLogAsync(dbContext, archiveFileId, "Hide", "Recorded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.Info(nameof(PhotoReviewRepository), $"Hid file '{archiveFileId}'.");
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

            if (TryGetPerceptualHashDistance(metadata?.PerceptualHash, row.candidateMetadata?.PerceptualHash, out var hashDistance))
            {
                if (hashDistance == 0)
                {
                    reasons.Add("same visual hash");
                    score += 35;
                }
                else if (hashDistance <= 8)
                {
                    reasons.Add("similar visual hash");
                    score += 25;
                }
            }

            if (TryGetAverageColorDistance(metadata?.AverageColorHex, row.candidateMetadata?.AverageColorHex, out var colorDistance)
                && colorDistance <= 48)
            {
                reasons.Add("similar average color");
                score += 8;
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

    private static bool TryGetPerceptualHashDistance(string? left, string? right, out int distance)
    {
        distance = 0;
        if (string.IsNullOrWhiteSpace(left)
            || string.IsNullOrWhiteSpace(right)
            || left.Length != right.Length
            || left.Any(character => character is not '0' and not '1')
            || right.Any(character => character is not '0' and not '1'))
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                distance++;
            }
        }

        return true;
    }

    private static bool TryGetAverageColorDistance(string? left, string? right, out int distance)
    {
        distance = 0;
        if (!TryParseHexColor(left, out var leftColor) || !TryParseHexColor(right, out var rightColor))
        {
            return false;
        }

        var redDelta = leftColor.Red - rightColor.Red;
        var greenDelta = leftColor.Green - rightColor.Green;
        var blueDelta = leftColor.Blue - rightColor.Blue;
        distance = (int)Math.Round(Math.Sqrt(redDelta * redDelta + greenDelta * greenDelta + blueDelta * blueDelta));
        return true;
    }

    private static bool TryParseHexColor(string? value, out (int Red, int Green, int Blue) color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.StartsWith('#') ? value[1..] : value;
        if (normalized.Length != 6
            || !int.TryParse(normalized[..2], System.Globalization.NumberStyles.HexNumber, null, out var red)
            || !int.TryParse(normalized[2..4], System.Globalization.NumberStyles.HexNumber, null, out var green)
            || !int.TryParse(normalized[4..6], System.Globalization.NumberStyles.HexNumber, null, out var blue))
        {
            return false;
        }

        color = (red, green, blue);
        return true;
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
            ThumbnailPath: file.ThumbnailPath,
            InferredTakenDate: metadata?.InferredTakenDate,
            DateConfidence: metadata?.DateConfidence ?? DateConfidence.Unknown,
            Title: metadata?.Title,
            Tags: tags);
    }

    private static async Task<ReviewPhotoPageSummary> GetPageSummaryAsync(
        PhotoArchiveDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var archiveFiles = await dbContext.ArchiveFiles.CountAsync(cancellationToken);
        var supportedImages = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.SupportedImage
                && file.Status != ArchiveFileStatus.Duplicate
                && file.Status != ArchiveFileStatus.Deleted,
            cancellationToken);
        var duplicateFiles = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.Duplicate || file.Status == ArchiveFileStatus.Duplicate,
            cancellationToken);
        var unsupportedFiles = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.Unsupported || file.MediaKind == MediaKind.Unknown,
            cancellationToken);
        var deletedFiles = await dbContext.ArchiveFiles.CountAsync(
            file => file.Status == ArchiveFileStatus.Deleted,
            cancellationToken);

        return new ReviewPhotoPageSummary(
            archiveFiles,
            supportedImages,
            duplicateFiles,
            unsupportedFiles,
            deletedFiles);
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
                .ThenBy(row => row.metadata?.InferredTakenDate)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase),
            ReviewSortMode.DateConfidence => rows
                .OrderBy(row => row.metadata?.DateConfidence ?? DateConfidence.Unknown)
                .ThenBy(row => row.metadata?.InferredTakenDate is null)
                .ThenBy(row => row.metadata?.InferredTakenDate)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase),
            _ => rows
                .OrderBy(row => row.metadata?.InferredTakenDate is null)
                .ThenBy(row => row.metadata?.InferredTakenDate)
                .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed record PhotoRow(ArchiveFile file, PhotoMetadata? metadata);
    private sealed record ResequenceMove(ArchiveFile File, string SourcePath, string DestinationPath);

    private async Task ResequenceAffectedDaysAsync(
        PhotoArchiveDbContext dbContext,
        DateTimeOffset? oldDate,
        DateTimeOffset newDate,
        CancellationToken cancellationToken)
    {
        var affectedDates = new HashSet<DateOnly> { DateOnly.FromDateTime(newDate.Date) };
        if (oldDate is not null)
        {
            affectedDates.Add(DateOnly.FromDateTime(oldDate.Value.Date));
        }

        foreach (var affectedDate in affectedDates)
        {
            await ResequenceDayAsync(dbContext, affectedDate, cancellationToken);
        }
    }

    private async Task ResequenceDayAsync(
        PhotoArchiveDbContext dbContext,
        DateOnly affectedDate,
        CancellationToken cancellationToken)
    {
        var rows = (await (from file in dbContext.ArchiveFiles
                           join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId
                           where file.MediaKind == MediaKind.SupportedImage
                               && file.Status != ArchiveFileStatus.Duplicate
                               && file.Status != ArchiveFileStatus.Deleted
                               && file.CurrentPath != null
                               && metadata.InferredTakenDate != null
                           select new { file, metadata })
            .ToListAsync(cancellationToken))
            .Where(row => DateOnly.FromDateTime(row.metadata.InferredTakenDate!.Value.Date) == affectedDate)
            .OrderBy(row => row.metadata.InferredTakenDate)
            .ThenBy(row => row.file.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.file.Sha256Hash, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sequence = 1;
        var moves = new List<ResequenceMove>();
        foreach (var row in rows)
        {
            if (!TryBuildResequencedPath(row.file.CurrentPath!, row.file.Extension, row.metadata.InferredTakenDate!.Value, sequence, out var resequencedPath))
            {
                sequence++;
                continue;
            }

            if (!string.Equals(row.file.CurrentPath, resequencedPath, StringComparison.OrdinalIgnoreCase))
            {
                moves.Add(new ResequenceMove(row.file, row.file.CurrentPath!, resequencedPath));
            }

            sequence++;
        }

        await ApplyResequenceMovesAsync(dbContext, moves, cancellationToken);
    }

    private static async Task ApplyResequenceMovesAsync(
        PhotoArchiveDbContext dbContext,
        IReadOnlyList<ResequenceMove> moves,
        CancellationToken cancellationToken)
    {
        if (moves.Count == 0)
        {
            return;
        }

        var sourcePaths = moves.Select(move => move.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocked = new HashSet<Guid>();
        foreach (var move in moves)
        {
            if (File.Exists(move.DestinationPath) && !sourcePaths.Contains(move.DestinationPath))
            {
                move.File.Status = ArchiveFileStatus.NeedsReview;
                move.File.UpdatedAtUtc = DateTimeOffset.UtcNow;
                blocked.Add(move.File.Id);
                await AddOperationLogAsync(dbContext, move.File.Id, "Resequence", "Collision", cancellationToken, move.DestinationPath);
            }
        }

        var physicalMoves = moves
            .Where(move => !blocked.Contains(move.File.Id) && File.Exists(move.SourcePath))
            .ToList();
        var tempMoves = new List<(ResequenceMove Move, string TempPath)>();

        foreach (var move in physicalMoves)
        {
            var tempPath = Path.Combine(
                Path.GetDirectoryName(move.SourcePath) ?? ".",
                $".photoarchive-resequence-{Guid.NewGuid():N}{Path.GetExtension(move.SourcePath)}");
            File.Move(move.SourcePath, tempPath);
            tempMoves.Add((move, tempPath));
        }

        try
        {
            foreach (var (move, tempPath) in tempMoves)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(move.DestinationPath) ?? ".");
                File.Move(tempPath, move.DestinationPath);
            }
        }
        catch
        {
            foreach (var (move, tempPath) in tempMoves.Where(item => File.Exists(item.TempPath)))
            {
                if (!File.Exists(move.SourcePath))
                {
                    File.Move(tempPath, move.SourcePath);
                }
            }

            throw;
        }

        foreach (var move in moves.Where(move => !blocked.Contains(move.File.Id)))
        {
            move.File.CurrentPath = move.DestinationPath;
            move.File.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var result = physicalMoves.Contains(move) ? "Renamed" : "Recorded";
            await AddOperationLogAsync(dbContext, move.File.Id, "Resequence", result, cancellationToken, move.DestinationPath);
        }
    }

    private bool TryBuildResequencedPath(
        string currentPath,
        string extension,
        DateTimeOffset takenDate,
        int sequence,
        out string resequencedPath)
    {
        resequencedPath = string.Empty;

        if (!TryGetOutputRoot(currentPath, out var outputRoot))
        {
            return false;
        }

        var normalizedExtension = extension.StartsWith(".", StringComparison.Ordinal)
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
        var fileName = $"{takenDate:yyyyMMdd} - {sequence}{normalizedExtension}";
        resequencedPath = Path.Combine(
            outputRoot,
            "Photos",
            decadeBucketPolicy.GetBucket(takenDate),
            takenDate.Year.ToString("0000"),
            fileName);
        return true;
    }

    private static bool TryGetOutputRoot(string currentPath, out string outputRoot)
    {
        var directory = Path.GetDirectoryName(currentPath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (string.Equals(Path.GetFileName(directory), "Photos", StringComparison.OrdinalIgnoreCase))
            {
                outputRoot = Path.GetDirectoryName(directory) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(outputRoot);
            }

            directory = Path.GetDirectoryName(directory);
        }

        outputRoot = string.Empty;
        return false;
    }

    private InvalidOperationException LogAndCreateInvalidOperation(string message, Guid fileId)
    {
        logger.Warning(nameof(PhotoReviewRepository), $"{message} FileId: {fileId}");
        return new InvalidOperationException(message);
    }

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
