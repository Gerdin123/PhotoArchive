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
                SourcePath = "C:\\input\\a,\"b\".jpg",
                OutputPath = "C:\\output\\a.jpg",
                Bucket = "Images",
                GroupingYear = 2024,
                GroupingDateSource = "DateTaken",
                GroupingDate = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                DateTaken = null,
                CreatedYear = 2024,
                CreatedAtUtc = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                LastWriteAtUtc = new DateTime(2024, 1, 2, 3, 4, 6, DateTimeKind.Utc),
                SizeBytes = 123,
                Extension = ".jpg",
                Sha256 = "ABC",
                IsDuplicate = false,
                CanonicalSourcePath = string.Empty
            };

            var csvPath = CsvExportService.Export(outputRoot, [record]);
            var lines = File.ReadAllLines(csvPath);

            Assert.Equal(2, lines.Length);
            Assert.StartsWith("SourcePath,OutputPath,Bucket", lines[0], StringComparison.Ordinal);
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
