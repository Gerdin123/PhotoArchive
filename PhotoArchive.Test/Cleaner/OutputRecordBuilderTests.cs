using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class OutputRecordBuilderTests
{
    [Fact]
    public void Build_UsesDuplicateBucket_WhenCanonicalPathDiffers()
    {
        var mover = new FakeMover();
        var report = new ReportService();
        var builder = new OutputRecordBuilder(
            groupThumbnails: true,
            groupLegacyProgramFiles: true,
            mover: mover,
            report: report);

        var analyzed = new AnalyzedFile
        {
            SourcePath = @"C:\in\a.jpg",
            FileType = FileType.Image,
            GroupingDate = new DateTime(2024, 1, 2),
            GroupingDateSource = "DateTaken",
            CreatedAtUtc = DateTime.UtcNow,
            LastWriteAtUtc = DateTime.UtcNow,
            Extension = ".jpg",
            Sha256 = "H1"
        };

        var record = builder.Build(analyzed, @"C:\in\other.jpg");

        Assert.Equal("Duplicates", record.Bucket);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(@"C:\in\other.jpg", record.CanonicalSourcePath);
        Assert.StartsWith("dup:", record.OutputPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UsesThumbnailCategory_ForOtherThumbnailFiles()
    {
        var mover = new FakeMover();
        var report = new ReportService();
        var builder = new OutputRecordBuilder(
            groupThumbnails: true,
            groupLegacyProgramFiles: false,
            mover: mover,
            report: report);

        var analyzed = new AnalyzedFile
        {
            SourcePath = @"C:\in\my_thumb.png",
            FileType = FileType.Other,
            GroupingDate = new DateTime(2024, 1, 2),
            GroupingDateSource = "FileCreationTime",
            CreatedAtUtc = DateTime.UtcNow,
            LastWriteAtUtc = DateTime.UtcNow,
            Extension = ".png",
            Sha256 = "H2"
        };

        var record = builder.Build(analyzed, analyzed.SourcePath);

        Assert.Equal("Others/Thumbnails", record.Bucket);
        Assert.Equal(1, report.Others);
        Assert.StartsWith("othercat:Thumbnails", record.OutputPath, StringComparison.Ordinal);
    }

    private sealed class FakeMover : IFileMover
    {
        public string MoveToDuplicates(string file) => $"dup:{file}";
        public string MoveToImages(string file, int year, int month) => $"img:{year:D4}-{month:D2}:{file}";
        public string MoveToOthers(string file, int year, int month) => $"other:{year:D4}-{month:D2}:{file}";
        public string MoveToOthersCategory(string file, string category) => $"othercat:{category}:{file}";
    }
}
