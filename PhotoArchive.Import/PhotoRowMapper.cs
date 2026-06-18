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
        var importBatchId = ManifestCsv.GetField(fields, indexes.ImportBatchId);
        var extension = ManifestCsv.GetField(fields, indexes.Extension);
        var sha256 = ManifestCsv.GetField(fields, indexes.Sha256);
        var perceptualHash = ManifestCsv.GetField(fields, indexes.PerceptualHash);

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

        var groupingDateSource = ParseGroupingDateSource(ManifestCsv.GetField(fields, indexes.CleanerBestDateSource));
        var exifDateTimeOriginal = ParseNullableDateTime(fields, indexes.ExifDateTimeOriginal);
        var exifCreateDate = ParseNullableDateTime(fields, indexes.ExifCreateDate);
        var exifModifyDate = ParseNullableDateTime(fields, indexes.ExifModifyDate);
        var folderDateCandidate = ParseNullableDateTime(fields, indexes.FolderDateCandidate);
        var widthFromCsv = ParseNullableInt(fields, indexes.Width);
        var heightFromCsv = ParseNullableInt(fields, indexes.Height);
        var orientation = ParseNullableInt(fields, indexes.Orientation);
        var cameraMake = ManifestCsv.GetField(fields, indexes.CameraMake);
        var cameraModel = ManifestCsv.GetField(fields, indexes.CameraModel);

        var canonicalSourcePath = ManifestCsv.GetField(fields, indexes.CanonicalSourcePath);
        var fileName = Path.GetFileName(outputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(sourcePath);
        }

        var width = widthFromCsv;
        var height = heightFromCsv;
        if (!width.HasValue || !height.HasValue)
        {
            var resolved = ImageDimensionReader.ResolveImageDimensions(outputPath, sourcePath);
            width ??= resolved.Width;
            height ??= resolved.Height;
        }

        photo = new Photo
        {
            SourcePath = sourcePath,
            OutputPath = outputPath,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unknown" : fileName,
            Extension = string.IsNullOrWhiteSpace(extension) ? Path.GetExtension(outputPath) : extension,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            ImportBatchId = string.IsNullOrWhiteSpace(importBatchId) ? null : importBatchId,
            PerceptualHash = string.IsNullOrWhiteSpace(perceptualHash) ? null : perceptualHash,
            Bucket = PhotoBucket.Images,
            IsDuplicate = false,
            IsReviewed = false,
            CanonicalSourcePath = string.IsNullOrWhiteSpace(canonicalSourcePath) ? null : canonicalSourcePath,
            GroupingYear = groupingYear,
            GroupingDateSource = groupingDateSource,
            GroupingDate = groupingDate,
            ExifDateTimeOriginal = exifDateTimeOriginal,
            ExifCreateDate = exifCreateDate,
            ExifModifyDate = exifModifyDate,
            FolderDateCandidate = folderDateCandidate,
            CreatedAtUtc = createdAtUtc,
            LastWriteAtUtc = lastWriteAtUtc,
            CameraMake = string.IsNullOrWhiteSpace(cameraMake) ? null : cameraMake,
            CameraModel = string.IsNullOrWhiteSpace(cameraModel) ? null : cameraModel,
            Orientation = orientation,
            Width = width,
            Height = height
        };

        return true;
    }

    private static GroupingDateSource ParseGroupingDateSource(string input)
    {
        if (input.StartsWith("DateTimeOriginal", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("DateTaken", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.DateTimeOriginal;
        }

        if (input.StartsWith("CreateDate", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.CreateDate;
        }

        if (input.StartsWith("FolderStructure", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("FolderNamePrefix", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.FolderStructure;
        }

        if (input.StartsWith("LastWriteTime", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.LastWriteTime;
        }

        return GroupingDateSource.FileCreationTime;
    }

    private static int? ParseNullableInt(IReadOnlyList<string> fields, int index)
    {
        var input = ManifestCsv.GetField(fields, index);
        return ManifestCsv.TryParseInt(input, out var value) ? value : null;
    }

    private static DateTime? ParseNullableDateTime(IReadOnlyList<string> fields, int index)
    {
        return ManifestCsv.TryParseNullableDateTime(ManifestCsv.GetField(fields, index), out var value)
            ? value
            : null;
    }
}
