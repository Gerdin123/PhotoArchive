using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services;

internal sealed class FileAnalyzer(
    string sourceRoot,
    IFileClassifier classifier,
    IMetadataExtractor metadataExtractor,
    IDuplicateDetector duplicateDetector)
{
    public AnalyzedFile Analyze(string file)
    {
        var fileType = classifier.Classify(file);
        var fileInfo = new FileInfo(file);
        var createdUtc = fileInfo.CreationTimeUtc;
        var createdLocal = fileInfo.CreationTime;
        var lastWriteUtc = fileInfo.LastWriteTimeUtc;
        var lastWriteLocal = fileInfo.LastWriteTime;
        var hash = duplicateDetector.ComputeHash(file);

        _ = metadataExtractor.TryExtract(file, out var metadata);
        var folderDateCandidate = default(DateTime);
        var hasFolderDate = FolderDateResolver.TryGetDateFromFolder(sourceRoot, file, createdLocal, out folderDateCandidate, out _);

        var groupingDate = SelectBestDate(
            metadata.ExifDateTimeOriginal,
            metadata.ExifCreateDate,
            hasFolderDate ? folderDateCandidate : null,
            createdLocal,
            lastWriteLocal,
            out var groupingDateSource);

        return new AnalyzedFile
        {
            SourcePath = file,
            FileType = fileType,
            GroupingDate = groupingDate,
            GroupingDateSource = groupingDateSource,
            ExifDateTimeOriginal = metadata.ExifDateTimeOriginal,
            ExifCreateDate = metadata.ExifCreateDate,
            ExifModifyDate = metadata.ExifModifyDate,
            FolderDateCandidate = hasFolderDate ? folderDateCandidate : null,
            CreatedAtUtc = createdUtc,
            LastWriteAtUtc = lastWriteUtc,
            SizeBytes = fileInfo.Length,
            Extension = fileInfo.Extension,
            Sha256 = hash,
            Width = metadata.Width,
            Height = metadata.Height,
            Orientation = metadata.Orientation,
            CameraMake = metadata.CameraMake,
            CameraModel = metadata.CameraModel,
            ExifTags = metadata.ExifTags
        };
    }

    private static DateTime SelectBestDate(
        DateTime? exifDateTimeOriginal,
        DateTime? exifCreateDate,
        DateTime? folderDate,
        DateTime creationTime,
        DateTime lastWriteTime,
        out string source)
    {
        if (exifDateTimeOriginal.HasValue)
        {
            source = "DateTimeOriginal";
            return exifDateTimeOriginal.Value;
        }

        if (exifCreateDate.HasValue)
        {
            source = "CreateDate";
            return exifCreateDate.Value;
        }

        if (folderDate.HasValue)
        {
            source = "FolderStructure";
            return folderDate.Value;
        }

        if (IsUsableFileSystemDate(creationTime))
        {
            source = "CreationTime";
            return creationTime;
        }

        source = "LastWriteTime";
        return lastWriteTime;
    }

    private static bool IsUsableFileSystemDate(DateTime value)
    {
        return value.Year >= 1900;
    }
}
