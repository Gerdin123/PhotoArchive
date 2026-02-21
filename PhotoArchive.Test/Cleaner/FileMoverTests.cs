using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class FileMoverTests
{
    [Fact]
    public void Constructor_CreatesTopLevelBuckets()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-source-{Guid.NewGuid():N}");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);

        try
        {
            _ = new FileMover(sourceRoot, outputRoot);

            Assert.True(Directory.Exists(Path.Combine(outputRoot, "Images")));
            Assert.True(Directory.Exists(Path.Combine(outputRoot, "Duplicates")));
            Assert.True(Directory.Exists(Path.Combine(outputRoot, "Others")));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, recursive: true);
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public void MoveToImages_CopiesFileToYearAndMonthFolder()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-source-{Guid.NewGuid():N}");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var sourceFile = Path.Combine(sourceRoot, "pic.jpg");
        File.WriteAllText(sourceFile, "hello");

        try
        {
            var mover = new FileMover(sourceRoot, outputRoot);

            var destination = mover.MoveToImages(sourceFile, 2024, 7);

            Assert.True(File.Exists(destination));
            Assert.Contains(Path.Combine("Images", "2024", "07"), destination, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("hello", File.ReadAllText(destination));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, recursive: true);
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public void MoveToDuplicates_AppendsSuffix_WhenNameCollides()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-source-{Guid.NewGuid():N}");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-output-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        var sourceFile = Path.Combine(sourceRoot, "dup.jpg");
        File.WriteAllText(sourceFile, "incoming");

        try
        {
            var mover = new FileMover(sourceRoot, outputRoot);
            var duplicatesFolder = Path.Combine(outputRoot, "Duplicates");
            var existing = Path.Combine(duplicatesFolder, "dup.jpg");
            File.WriteAllText(existing, "existing");

            var destination = mover.MoveToDuplicates(sourceFile);

            Assert.EndsWith("dup_1.jpg", destination, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(destination));
            Assert.Equal("existing", File.ReadAllText(existing));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, recursive: true);
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }
}
