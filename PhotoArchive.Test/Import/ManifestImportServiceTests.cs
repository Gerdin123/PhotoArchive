using Microsoft.EntityFrameworkCore;
using PhotoArchive.Import;
using PhotoArchive.Test.Infrastructure;

namespace PhotoArchive.Test.Import;

public class ManifestImportServiceTests
{
    [Fact]
    public void ImportManifest_ReturnsExpectedSummary_AndImportsValidRows()
    {
        using var scope = TestDbContextFactory.Create();
        var tempDir = Path.Combine(Path.GetTempPath(), $"photoarchive-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "cleaned_manifest.csv");

        try
        {
            WriteManifest(manifestPath);
            var service = new ManifestImportService();

            var summary = service.ImportManifest(scope.Context, manifestPath);
            var imported = scope.Context.Photos.AsNoTracking().ToList();

            Assert.Equal(1, summary.Imported);
            Assert.Equal(2, summary.SkippedFiltered);
            Assert.Equal(1, summary.SkippedDuplicateHash);
            Assert.Equal(2, summary.SkippedInvalid);
            Assert.Single(imported);
            Assert.Equal("H1", imported[0].Sha256);
            Assert.False(imported[0].IsReviewed);
            var importedWithTags = scope.Context.Photos
                .AsNoTracking()
                .Include(p => p.PhotoTags)
                .ThenInclude(pt => pt.Tag)
                .Single();
            var tagNames = importedWithTags.PhotoTags
                .Select(x => x.Tag?.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.Equal(["Family", "Travel"], tagNames);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ImportManifest_Throws_WhenRequiredColumnMissing()
    {
        using var scope = TestDbContextFactory.Create();
        var tempDir = Path.Combine(Path.GetTempPath(), $"photoarchive-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "cleaned_manifest.csv");

        try
        {
            File.WriteAllLines(manifestPath,
            [
                "ImportBatchId,SourcePath,OutputPath,Bucket,Sha256,SizeBytes,Extension,Width,Height,Orientation,CameraMake,CameraModel,ExifTags,ExifDateTimeOriginal,ExifCreateDate,ExifModifyDate,FolderDateCandidate,CreatedAtUtc,LastWriteAtUtc,CleanerBestDate,CleanerBestDateSource,GroupingYear,GroupingDate,IsDuplicate",
                @"batch1,C:\a.jpg,C:\out\a.jpg,Images,H1,12,.jpg,640,480,1,Canon,EOS,Travel;Family,2024-01-01T00:00:00.0000000Z,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,DateTimeOriginal,2024,2024-01-01T00:00:00.0000000Z,false"
            ]);

            var service = new ManifestImportService();

            Assert.Throws<InvalidOperationException>(() => service.ImportManifest(scope.Context, manifestPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static void WriteManifest(string path)
    {
        var lines = new[]
        {
            "ImportBatchId,SourcePath,OutputPath,Bucket,Sha256,SizeBytes,Extension,Width,Height,Orientation,CameraMake,CameraModel,ExifTags,ExifDateTimeOriginal,ExifCreateDate,ExifModifyDate,FolderDateCandidate,CreatedAtUtc,LastWriteAtUtc,CleanerBestDate,CleanerBestDateSource,GroupingYear,GroupingDate,IsDuplicate,CanonicalSourcePath,PerceptualHash",
            @"batch1,C:\a.jpg,C:\out\a.jpg,Images,H1,12,.jpg,640,480,1,Canon,EOS,Travel;Family,2024-01-01T00:00:00.0000000Z,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,DateTimeOriginal,2024,2024-01-01T00:00:00.0000000Z,false,,",
            @"batch1,C:\b.jpg,C:\out\b.jpg,Images,H1,12,.jpg,640,480,1,Canon,EOS,,,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,CreationTime,2024,2024-01-01T00:00:00.0000000Z,false,,",
            @"batch1,C:\c.txt,C:\out\c.txt,Others,H2,12,.txt,,,,,,,,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,CreationTime,2024,2024-01-01T00:00:00.0000000Z,false,,",
            @"batch1,C:\d.jpg,C:\out\d.jpg,Images,H3,12,.jpg,640,480,1,Canon,EOS,,,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,DateTimeOriginal,2024,2024-01-01T00:00:00.0000000Z,true,C:\x.jpg,",
            @"batch1,C:\e.jpg,C:\out\e.jpg,Images,,12,.jpg,640,480,1,Canon,EOS,,,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,DateTimeOriginal,2024,2024-01-01T00:00:00.0000000Z,false,,",
            @"batch1,C:\f.jpg,C:\out\f.jpg,Images,H6,NaN,.jpg,640,480,1,Canon,EOS,,,,,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,2024-01-01T00:00:00.0000000Z,DateTimeOriginal,2024,2024-01-01T00:00:00.0000000Z,false,,"
        };

        File.WriteAllLines(path, lines);
    }
}
