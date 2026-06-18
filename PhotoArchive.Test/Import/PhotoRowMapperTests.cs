using PhotoArchive.Import;
using PhotoArchive.Domain.Entities;

namespace PhotoArchive.Test.Import;

public class PhotoRowMapperTests
{
    private static readonly ManifestColumnIndexes Indexes = new(
        ImportBatchId: 0,
        SourcePath: 1,
        OutputPath: 2,
        Bucket: 3,
        Sha256: 4,
        SizeBytes: 5,
        Extension: 6,
        Width: 7,
        Height: 8,
        Orientation: 9,
        CameraMake: 10,
        CameraModel: 11,
        ExifTags: 12,
        ExifDateTimeOriginal: 13,
        ExifCreateDate: 14,
        ExifModifyDate: 15,
        FolderDateCandidate: 16,
        CreatedAtUtc: 17,
        LastWriteAtUtc: 18,
        CleanerBestDate: 19,
        CleanerBestDateSource: 20,
        GroupingYear: 21,
        GroupingDate: 22,
        IsDuplicate: 23,
        CanonicalSourcePath: 24,
        PerceptualHash: 25);

    [Fact]
    public void TryCreatePhoto_MapsFields_AndFallsBackGroupingYear()
    {
        var fields = new List<string>
        {
            "batch-1",
            "C:\\src\\a.jpg",
            "C:\\out\\a.jpg",
            "Images",
            "ABC123",
            "42",
            ".jpg",
            "640",
            "480",
            "1",
            "Canon",
            "EOS",
            "Travel;Family",
            "2024-03-04T05:06:07.0000000Z",
            "",
            "",
            "",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:08.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "FolderStructure",
            "invalid",
            "2024-03-04T05:06:07.0000000Z",
            "false",
            "",
            "",
            ""
        };

        var ok = PhotoRowMapper.TryCreatePhoto(fields, Indexes, out var photo, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(2024, photo.GroupingYear);
        Assert.Equal("a.jpg", photo.FileName);
        Assert.Equal(".jpg", photo.Extension);
        Assert.Equal("ABC123", photo.Sha256);
        Assert.Equal(640, photo.Width);
        Assert.Equal(480, photo.Height);
        Assert.Equal(GroupingDateSource.FolderStructure, photo.GroupingDateSource);
        Assert.False(photo.IsReviewed);
    }

    [Fact]
    public void TryCreatePhoto_ReturnsFalse_ForInvalidSizeBytes()
    {
        var fields = new List<string>
        {
            "batch-1","src","out","Images","hash","NaN",".jpg","","","","","","","","","",
            "",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "DateTimeOriginal",
            "2024",
            "2024-03-04T05:06:07.0000000Z",
            "false","","",""
        };

        var ok = PhotoRowMapper.TryCreatePhoto(fields, Indexes, out _, out var error);

        Assert.False(ok);
        Assert.Equal("invalid SizeBytes value.", error);
    }
}
