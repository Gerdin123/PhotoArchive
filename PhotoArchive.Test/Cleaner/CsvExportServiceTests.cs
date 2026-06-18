using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class CsvExportServiceTests
{
    [Fact]
    public void Export_WritesHeader_AndEscapesSpecialCharacters()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"photoarchive-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputRoot);

        try
        {
            var record = new ProcessedFileRecord
            {
                ImportBatchId = "batch-1",
                SourcePath = "C:\\input\\a,\"b\".jpg",
                OutputPath = "C:\\output\\a.jpg",
                Bucket = "Images",
                Sha256 = "ABC",
                SizeBytes = 123,
                Extension = ".jpg",
                Width = 100,
                Height = 200,
                Orientation = 1,
                CameraMake = "Canon",
                CameraModel = "EOS",
                ExifDateTimeOriginal = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                ExifCreateDate = null,
                ExifModifyDate = null,
                FolderDateCandidate = null,
                CreatedAtUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                LastWriteAtUtc = new DateTime(2024, 1, 2, 3, 4, 6, DateTimeKind.Utc),
                CleanerBestDate = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                CleanerBestDateSource = "DateTimeOriginal",
                GroupingYear = 2024,
                GroupingDate = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                IsDuplicate = false,
                CanonicalSourcePath = string.Empty,
                PerceptualHash = string.Empty
            };

            var csvPath = CsvExportService.Export(outputRoot, [record]);
            var lines = File.ReadAllLines(csvPath);

            Assert.Equal(2, lines.Length);
            Assert.StartsWith("ImportBatchId,SourcePath,OutputPath,Bucket,Sha256", lines[0], StringComparison.Ordinal);
            Assert.Contains("\"C:\\input\\a,\"\"b\"\".jpg\"", lines[1], StringComparison.Ordinal);
            Assert.Contains(",false,", lines[1], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
                Directory.Delete(outputRoot, recursive: true);
        }
    }
}
