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
                "SourcePath,OutputPath,Bucket,GroupingYear,GroupingDateSource,GroupingDate,DateTaken,CreatedAtUtc,LastWriteAtUtc,SizeBytes,Extension,Sha256,IsDuplicate",
                @"C:\a.jpg,C:\out\a.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.jpg,H1,false"
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
            "SourcePath,OutputPath,Bucket,GroupingYear,GroupingDateSource,GroupingDate,DateTaken,CreatedAtUtc,LastWriteAtUtc,SizeBytes,Extension,Sha256,IsDuplicate,CanonicalSourcePath",
            @"C:\a.jpg,C:\out\a.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.jpg,H1,false,",
            @"C:\b.jpg,C:\out\b.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.jpg,H1,false,",
            @"C:\c.txt,C:\out\c.txt,Others,2024,FileCreationTime,2024-01-01T00:00:00.0000000Z,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.txt,H2,false,",
            @"C:\d.jpg,C:\out\d.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.jpg,H3,true,C:\x.jpg",
            @"C:\e.jpg,C:\out\e.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,12,.jpg,,false,",
            @"C:\f.jpg,C:\out\f.jpg,Images,2024,DateTaken,2024-01-01T00:00:00.0000000Z,,2024-01-01T00:00:00.0000000Z,2024-01-01T00:00:01.0000000Z,NaN,.jpg,H6,false,"
        };

        File.WriteAllLines(path, lines);
    }
}
