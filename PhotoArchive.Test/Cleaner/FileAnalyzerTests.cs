using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class FileAnalyzerTests
{
    [Fact]
    public void Analyze_UsesFolderDate_WhenEnabledAndAvailable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-analyze-{Guid.NewGuid():N}");
        var datedFolder = Path.Combine(root, "202402_trip");
        Directory.CreateDirectory(datedFolder);
        var file = Path.Combine(datedFolder, "a.jpg");
        File.WriteAllText(file, "abc");

        try
        {
            var analyzer = new FileAnalyzer(
                sourceRoot: root,
                useFolderDate: true,
                classifier: new StubClassifier(FileType.Image),
                metadataExtractor: new StubMetadataExtractor(false, default),
                duplicateDetector: new StubDuplicateDetector("HASH"));

            var result = analyzer.Analyze(file);

            Assert.Equal("HASH", result.Sha256);
            Assert.Equal("FolderNamePrefix(yyyyMM)", result.GroupingDateSource);
            Assert.Equal(2024, result.GroupingDate.Year);
            Assert.Equal(2, result.GroupingDate.Month);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyze_UsesDateTaken_WhenFolderDateDisabled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-analyze-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "a.jpg");
        File.WriteAllText(file, "abc");
        var dateTaken = new DateTime(2023, 5, 6, 7, 8, 9);

        try
        {
            var analyzer = new FileAnalyzer(
                sourceRoot: root,
                useFolderDate: false,
                classifier: new StubClassifier(FileType.Image),
                metadataExtractor: new StubMetadataExtractor(true, dateTaken),
                duplicateDetector: new StubDuplicateDetector("HASH"));

            var result = analyzer.Analyze(file);

            Assert.Equal("DateTaken", result.GroupingDateSource);
            Assert.Equal(dateTaken, result.GroupingDate);
            Assert.Equal(dateTaken, result.DateTaken);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubClassifier(FileType output) : IFileClassifier
    {
        public FileType Classify(string filename) => output;
    }

    private sealed class StubMetadataExtractor(bool hasDateTaken, DateTime date) : IMetadataExtractor
    {
        public bool TryGetDateTaken(string filePath, out DateTime dateTaken)
        {
            dateTaken = date;
            return hasDateTaken;
        }
    }

    private sealed class StubDuplicateDetector(string hash) : IDuplicateDetector
    {
        public string ComputeHash(string filePath) => hash;

        public DuplicateCheckResult Register(string filePath)
        {
            throw new NotSupportedException();
        }
    }
}
