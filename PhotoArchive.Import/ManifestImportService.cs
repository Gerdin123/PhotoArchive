using PhotoArchive.Infrastructure;

namespace PhotoArchive.Import;

internal sealed class ManifestImportService
{
    public ImportSummary ImportManifest(PhotoArchiveDbContext dbContext, string manifestPath)
    {
        var totalRows = ManifestCsv.CountDataRows(manifestPath);
        using var reader = new StreamReader(manifestPath);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new ImportSummary();
        }

        var headers = ManifestCsv.ParseCsvLine(headerLine);
        var headerIndex = ManifestCsv.BuildHeaderIndex(headers);
        var indexes = ManifestColumnIndexes.FromHeaderIndex(headerIndex);

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        var imported = 0;
        var skippedFiltered = 0;
        var skippedDuplicateHash = 0;
        var skippedInvalid = 0;
        var processed = 0;
        var pending = 0;

        var importedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 1;
        using var progress = new ConsoleProgressReporter(totalRows);
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            processed++;
            var fields = ManifestCsv.ParseCsvLine(line);
            if (fields.Count < headers.Count)
            {
                skippedInvalid++;
                progress.WriteMessage($"Skipping line {lineNumber}: column count mismatch.");
                progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid, force: true);
                continue;
            }

            var bucket = ManifestCsv.GetField(fields, indexes.Bucket);
            var isDuplicate = ManifestCsv.ParseBoolean(ManifestCsv.GetField(fields, indexes.IsDuplicate));
            if (!bucket.Equals("Images", StringComparison.OrdinalIgnoreCase) || isDuplicate)
            {
                skippedFiltered++;
                progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid);
                continue;
            }

            var hash = ManifestCsv.GetField(fields, indexes.Sha256);
            if (string.IsNullOrWhiteSpace(hash))
            {
                skippedInvalid++;
                progress.WriteMessage($"Skipping line {lineNumber}: missing Sha256.");
                progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid, force: true);
                continue;
            }

            if (!importedHashes.Add(hash))
            {
                skippedDuplicateHash++;
                progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid);
                continue;
            }

            if (!PhotoRowMapper.TryCreatePhoto(fields, indexes, out var photo, out var validationError))
            {
                skippedInvalid++;
                progress.WriteMessage($"Skipping line {lineNumber}: {validationError}");
                progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid, force: true);
                continue;
            }

            dbContext.Photos.Add(photo);
            imported++;
            pending++;
            progress.Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid);

            if (pending >= 500)
            {
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();
                pending = 0;
            }
        }

        if (pending > 0)
        {
            dbContext.SaveChanges();
        }

        progress.Complete(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid);

        return new ImportSummary
        {
            Imported = imported,
            SkippedFiltered = skippedFiltered,
            SkippedDuplicateHash = skippedDuplicateHash,
            SkippedInvalid = skippedInvalid
        };
    }
}
