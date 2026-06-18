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
                classifier: new StubClassifier(FileType.Image),
                metadataExtractor: new StubMetadataExtractor(new ExtractedMetadata()),
                duplicateDetector: new StubDuplicateDetector("HASH"));

            var result = analyzer.Analyze(file);

            Assert.Equal("HASH", result.Sha256);
            Assert.Equal("FolderStructure", result.GroupingDateSource);
            Assert.Equal(2024, result.GroupingDate.Year);
            Assert.Equal(2, result.GroupingDate.Month);
            Assert.NotNull(result.FolderDateCandidate);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Analyze_UsesDateTimeOriginal_WhenExifDateExists()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-analyze-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "a.jpg");
        File.WriteAllText(file, "abc");
        var exifDate = new DateTime(2023, 5, 6, 7, 8, 9);

        try
        {
            var analyzer = new FileAnalyzer(
                sourceRoot: root,
                classifier: new StubClassifier(FileType.Image),
                metadataExtractor: new StubMetadataExtractor(new ExtractedMetadata
                {
                    ExifDateTimeOriginal = exifDate
                }),
                duplicateDetector: new StubDuplicateDetector("HASH"));

            var result = analyzer.Analyze(file);

            Assert.Equal("DateTimeOriginal", result.GroupingDateSource);
            Assert.Equal(exifDate, result.GroupingDate);
            Assert.Equal(exifDate, result.ExifDateTimeOriginal);
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

    private sealed class StubMetadataExtractor(ExtractedMetadata metadata) : IMetadataExtractor
    {
        public bool TryExtract(string filePath, out ExtractedMetadata extracted)
        {
            extracted = metadata;
            return true;
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
