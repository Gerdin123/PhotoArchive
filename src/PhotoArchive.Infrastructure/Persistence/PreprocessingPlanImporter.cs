using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using Microsoft.EntityFrameworkCore;

namespace PhotoArchive.Infrastructure.Persistence;

public sealed class PreprocessingPlanImporter
{
    public async Task<PreprocessingImportResult> ImportAsync(
        PhotoArchiveDbContext dbContext,
        OutputPlan plan,
        CancellationToken cancellationToken = default)
    {
        var fileBySource = new Dictionary<string, ArchiveFile>(StringComparer.OrdinalIgnoreCase);
        var sourcePaths = plan.Operations.Select(operation => operation.SourcePath).ToArray();
        var existingFiles = await dbContext.ArchiveFiles
            .Where(file => sourcePaths.Contains(file.OriginalPath))
            .ToDictionaryAsync(file => file.OriginalPath, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var operation in plan.Operations)
        {
            var info = new FileInfo(operation.SourcePath);
            var archiveFile = existingFiles.TryGetValue(operation.SourcePath, out var existingFile)
                ? existingFile
                : new ArchiveFile
                {
                    OriginalPath = operation.SourcePath,
                    OriginalFileName = Path.GetFileName(operation.SourcePath),
                    Extension = Path.GetExtension(operation.SourcePath),
                    FileSizeBytes = info.Exists ? info.Length : 0
                };

            archiveFile.CurrentPath = operation.ExecutionResult == "Copied" ? operation.DestinationPath : archiveFile.CurrentPath;
            archiveFile.Sha256Hash = operation.Sha256Hash;
            archiveFile.MediaKind = operation.MediaKind;
            archiveFile.Status = ToStatus(operation);
            archiveFile.UpdatedAtUtc = plan.RunStartedAtUtc;

            if (!existingFiles.ContainsKey(operation.SourcePath))
            {
                dbContext.ArchiveFiles.Add(archiveFile);
            }

            fileBySource[operation.SourcePath] = archiveFile;

            var metadata = await dbContext.PhotoMetadata.FindAsync([archiveFile.Id], cancellationToken);
            if (metadata is null)
            {
                metadata = new PhotoMetadata { ArchiveFileId = archiveFile.Id };
                dbContext.PhotoMetadata.Add(metadata);
            }

            metadata.InferredTakenDate = operation.InferredTakenDate;
            metadata.DateConfidence = operation.DateConfidence;
            metadata.FileCreatedDate = info.Exists ? new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero) : null;
            metadata.FileModifiedDate = info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : null;

            dbContext.OperationLogs.Add(new OperationLog
            {
                OperationType = operation.ExecutionResult == "Planned" ? "Plan" : "Copy",
                SourcePath = operation.SourcePath,
                DestinationPath = operation.DestinationPath,
                Result = operation.ExecutionResult,
                ErrorMessage = operation.ErrorMessage,
                CreatedAtUtc = plan.RunStartedAtUtc
            });
        }

        foreach (var duplicateGroup in plan.Operations
            .Where(operation => operation.DuplicateGroupId is not null)
            .GroupBy(operation => operation.DuplicateGroupId!, StringComparer.OrdinalIgnoreCase))
        {
            var first = duplicateGroup.First();
            Guid? canonicalFileId = null;
            if (first.CanonicalSourcePath is not null
                && fileBySource.TryGetValue(first.CanonicalSourcePath, out var canonicalFile))
            {
                canonicalFileId = canonicalFile.Id;
            }

            var existingGroup = await dbContext.DuplicateGroups
                .SingleOrDefaultAsync(group => group.Hash == duplicateGroup.Key, cancellationToken);

            if (existingGroup is null)
            {
                dbContext.DuplicateGroups.Add(new DuplicateGroup
                {
                    Hash = duplicateGroup.Key,
                    CanonicalFileId = canonicalFileId,
                    CreatedAtUtc = plan.RunStartedAtUtc
                });
            }
            else
            {
                existingGroup.CanonicalFileId = canonicalFileId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PreprocessingImportResult(
            ArchiveFiles: plan.Operations.Count,
            MetadataRows: plan.Operations.Count,
            DuplicateGroups: plan.Operations.Count(operation => operation.DuplicateGroupId is not null)
                == 0
                    ? 0
                    : plan.Operations
                        .Where(operation => operation.DuplicateGroupId is not null)
                        .Select(operation => operation.DuplicateGroupId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count(),
            OperationLogs: plan.Operations.Count);
    }

    private static ArchiveFileStatus ToStatus(PlannedFileOperation operation)
    {
        if (operation.ExecutionResult == "Planned")
        {
            return ArchiveFileStatus.Planned;
        }

        if (operation.ExecutionResult == "Failed")
        {
            return ArchiveFileStatus.NeedsReview;
        }

        return operation.IsDuplicate ? ArchiveFileStatus.Duplicate : ArchiveFileStatus.Processed;
    }
}
