using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class DuplicateDetectorTests
{
    [Fact]
    public void Register_FirstFileIsNotDuplicate_SecondMatchingFileIsDuplicate()
    {
        var detector = new DuplicateDetector();
        var tempDir = Path.Combine(Path.GetTempPath(), $"photoarchive-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var firstFile = Path.Combine(tempDir, "a.jpg");
        var secondFile = Path.Combine(tempDir, "b.jpg");

        File.WriteAllText(firstFile, "same-content");
        File.WriteAllText(secondFile, "same-content");

        try
        {
            var first = detector.Register(firstFile);
            var second = detector.Register(secondFile);

            Assert.False(first.IsDuplicate);
            Assert.Null(first.CanonicalPath);
            Assert.False(string.IsNullOrWhiteSpace(first.Hash));

            Assert.True(second.IsDuplicate);
            Assert.Equal(firstFile, second.CanonicalPath);
            Assert.Equal(first.Hash, second.Hash);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Register_DifferentContent_IsNotDuplicate()
    {
        var detector = new DuplicateDetector();
        var tempDir = Path.Combine(Path.GetTempPath(), $"photoarchive-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var firstFile = Path.Combine(tempDir, "a.jpg");
        var secondFile = Path.Combine(tempDir, "b.jpg");

        File.WriteAllText(firstFile, "content-1");
        File.WriteAllText(secondFile, "content-2");

        try
        {
            var first = detector.Register(firstFile);
            var second = detector.Register(secondFile);

            Assert.False(first.IsDuplicate);
            Assert.False(second.IsDuplicate);
            Assert.NotEqual(first.Hash, second.Hash);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
