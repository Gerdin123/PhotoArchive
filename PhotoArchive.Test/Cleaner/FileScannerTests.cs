using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class FileScannerTests
{
    [Fact]
    public void ScanRecursively_ReturnsFilesFromNestedDirectories()
    {
        var scanner = new FileScanner();
        var tempDir = Path.Combine(Path.GetTempPath(), $"photoarchive-scan-{Guid.NewGuid():N}");

        var nestedDir = Path.Combine(tempDir, "a", "b");
        Directory.CreateDirectory(nestedDir);

        var rootFile = Path.Combine(tempDir, "root.txt");
        var nestedFile = Path.Combine(nestedDir, "nested.txt");
        File.WriteAllText(rootFile, "root");
        File.WriteAllText(nestedFile, "nested");

        try
        {
            var files = scanner.ScanRecursively(tempDir).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.Contains(rootFile, files);
            Assert.Contains(nestedFile, files);
            Assert.Equal(2, files.Count);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
