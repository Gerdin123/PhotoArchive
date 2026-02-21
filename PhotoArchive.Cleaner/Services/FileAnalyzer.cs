using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services;

internal sealed class FileAnalyzer(
    string sourceRoot,
    bool useFolderDate,
    IFileClassifier classifier,
    IMetadataExtractor metadataExtractor,
    IDuplicateDetector duplicateDetector)
{
    public AnalyzedFile Analyze(string file)
    {
        var fileType = classifier.Classify(file);
        var fileInfo = new FileInfo(file);
        var createdUtc = fileInfo.CreationTimeUtc;
        var hash = duplicateDetector.ComputeHash(file);

        var hasDateTaken = metadataExtractor.TryGetDateTaken(file, out var dateTaken);
        var fallbackDate = hasDateTaken ? dateTaken : fileInfo.CreationTime;
        var folderDate = default(DateTime);
        var folderDateSource = string.Empty;
        var hasFolderDate = useFolderDate && FolderDateResolver.TryGetDateFromFolder(sourceRoot, file, fallbackDate, out folderDate, out folderDateSource);
        var groupingDate = hasFolderDate ? folderDate : fallbackDate;
        var groupingDateSource = hasFolderDate ? folderDateSource : (hasDateTaken ? "DateTaken" : "FileCreationTime");

        return new AnalyzedFile
        {
            SourcePath = file,
            FileType = fileType,
            GroupingDate = groupingDate,
            GroupingDateSource = groupingDateSource,
            DateTaken = hasDateTaken ? dateTaken : null,
            CreatedAtUtc = createdUtc,
            LastWriteAtUtc = fileInfo.LastWriteTimeUtc,
            SizeBytes = fileInfo.Length,
            Extension = fileInfo.Extension,
            Sha256 = hash
        };
    }
}
