using Microsoft.EntityFrameworkCore;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure.Metadata;

namespace PhotoArchive.Infrastructure.Persistence;

public sealed class MetadataWriteBackService
{
    private readonly IMetadataWriter metadataWriter;

    public MetadataWriteBackService(IMetadataWriter metadataWriter)
    {
        this.metadataWriter = metadataWriter;
    }

    public async Task<MetadataWriteBackResult> WriteAsync(
        PhotoArchiveDbContext dbContext,
        bool onlyCorrected = true,
        CancellationToken cancellationToken = default)
    {
        var correctedFileIds = onlyCorrected
            ? await dbContext.ManualCorrections
                .Where(correction => correction.FieldName == nameof(PhotoMetadata.InferredTakenDate))
                .Select(correction => correction.ArchiveFileId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : null;

        var query = from file in dbContext.ArchiveFiles
                    join metadata in dbContext.PhotoMetadata on file.Id equals metadata.ArchiveFileId
                    where file.MediaKind == MediaKind.SupportedImage
                          && metadata.InferredTakenDate != null
                    select new { file, metadata };

        if (correctedFileIds is not null)
        {
            query = query.Where(row => correctedFileIds.Contains(row.file.Id));
        }

        var rows = await query.ToListAsync(cancellationToken);
        var written = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var row in rows)
        {
            var targetPath = row.file.CurrentPath ?? row.file.OriginalPath;
            if (!File.Exists(targetPath))
            {
                skipped++;
                dbContext.OperationLogs.Add(new OperationLog
                {
                    OperationType = "MetadataWriteBack",
                    SourcePath = targetPath,
                    Result = "Skipped",
                    ErrorMessage = "Target file does not exist."
                });
                continue;
            }

            try
            {
                await metadataWriter.WriteAsync(
                    new MetadataWriteRequest(targetPath, row.metadata.InferredTakenDate, PreferSidecar: true),
                    cancellationToken);

                written++;
                dbContext.OperationLogs.Add(new OperationLog
                {
                    OperationType = "MetadataWriteBack",
                    SourcePath = targetPath,
                    DestinationPath = XmpSidecarMetadataWriter.GetSidecarPath(targetPath),
                    Result = "Written"
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                failed++;
                dbContext.OperationLogs.Add(new OperationLog
                {
                    OperationType = "MetadataWriteBack",
                    SourcePath = targetPath,
                    DestinationPath = XmpSidecarMetadataWriter.GetSidecarPath(targetPath),
                    Result = "Failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new MetadataWriteBackResult(
            Attempted: rows.Count,
            Written: written,
            Skipped: skipped,
            Failed: failed);
    }
}
