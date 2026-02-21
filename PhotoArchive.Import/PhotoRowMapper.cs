using PhotoArchive.Domain.Entities;

namespace PhotoArchive.Import;

internal static class PhotoRowMapper
{
    public static bool TryCreatePhoto(
        IReadOnlyList<string> fields,
        ManifestColumnIndexes indexes,
        out Photo photo,
        out string error)
    {
        photo = null!;
        error = string.Empty;

        var sourcePath = ManifestCsv.GetField(fields, indexes.SourcePath);
        var outputPath = ManifestCsv.GetField(fields, indexes.OutputPath);
        var extension = ManifestCsv.GetField(fields, indexes.Extension);
        var sha256 = ManifestCsv.GetField(fields, indexes.Sha256);

        if (!ManifestCsv.TryParseLong(ManifestCsv.GetField(fields, indexes.SizeBytes), out var sizeBytes))
        {
            error = "invalid SizeBytes value.";
            return false;
        }

        if (!ManifestCsv.TryParseDateTime(ManifestCsv.GetField(fields, indexes.GroupingDate), out var groupingDate))
        {
            error = "invalid GroupingDate value.";
            return false;
        }

        if (!ManifestCsv.TryParseDateTime(ManifestCsv.GetField(fields, indexes.CreatedAtUtc), out var createdAtUtc))
        {
            error = "invalid CreatedAtUtc value.";
            return false;
        }

        if (!ManifestCsv.TryParseDateTime(ManifestCsv.GetField(fields, indexes.LastWriteAtUtc), out var lastWriteAtUtc))
        {
            error = "invalid LastWriteAtUtc value.";
            return false;
        }

        var groupingYearText = ManifestCsv.GetField(fields, indexes.GroupingYear);
        var groupingYear = ManifestCsv.TryParseInt(groupingYearText, out var parsedYear) ? parsedYear : groupingDate.Year;

        var groupingDateSource = ParseGroupingDateSource(ManifestCsv.GetField(fields, indexes.GroupingDateSource));
        var dateTaken = ManifestCsv.TryParseNullableDateTime(ManifestCsv.GetField(fields, indexes.DateTaken), out var parsedDateTaken)
            ? parsedDateTaken
            : null;

        var canonicalSourcePath = ManifestCsv.GetField(fields, indexes.CanonicalSourcePath);
        var fileName = Path.GetFileName(outputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(sourcePath);
        }

        var (width, height) = ImageDimensionReader.ResolveImageDimensions(outputPath, sourcePath);

        photo = new Photo
        {
            SourcePath = sourcePath,
            OutputPath = outputPath,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unknown" : fileName,
            Extension = string.IsNullOrWhiteSpace(extension) ? Path.GetExtension(outputPath) : extension,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Bucket = PhotoBucket.Images,
            IsDuplicate = false,
            CanonicalSourcePath = string.IsNullOrWhiteSpace(canonicalSourcePath) ? null : canonicalSourcePath,
            GroupingYear = groupingYear,
            GroupingDateSource = groupingDateSource,
            GroupingDate = groupingDate,
            DateTaken = dateTaken,
            CreatedAtUtc = createdAtUtc,
            LastWriteAtUtc = lastWriteAtUtc,
            Width = width,
            Height = height
        };

        return true;
    }

    private static GroupingDateSource ParseGroupingDateSource(string input)
    {
        if (input.StartsWith("DateTaken", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.DateTaken;
        }

        if (input.StartsWith("FolderNamePrefix", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.FolderNamePrefix;
        }

        return GroupingDateSource.FileCreationTime;
    }
}
