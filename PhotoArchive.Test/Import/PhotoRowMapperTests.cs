using PhotoArchive.Import;

namespace PhotoArchive.Test.Import;

public class PhotoRowMapperTests
{
    private static readonly ManifestColumnIndexes Indexes = new(
        SourcePath: 0,
        OutputPath: 1,
        Bucket: 2,
        GroupingYear: 3,
        GroupingDateSource: 4,
        GroupingDate: 5,
        DateTaken: 6,
        CreatedAtUtc: 7,
        LastWriteAtUtc: 8,
        SizeBytes: 9,
        Extension: 10,
        Sha256: 11,
        IsDuplicate: 12,
        CanonicalSourcePath: 13);

    [Fact]
    public void TryCreatePhoto_MapsFields_AndFallsBackGroupingYear()
    {
        var fields = new List<string>
        {
            "C:\\src\\a.jpg",
            "C:\\out\\a.jpg",
            "Images",
            "invalid",
            "FolderNamePrefix(yyyyMM)",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:08.0000000Z",
            "42",
            ".jpg",
            "ABC123",
            "false",
            ""
        };

        var ok = PhotoRowMapper.TryCreatePhoto(fields, Indexes, out var photo, out var error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
        Assert.Equal(2024, photo.GroupingYear);
        Assert.Equal("a.jpg", photo.FileName);
        Assert.Equal(".jpg", photo.Extension);
        Assert.Equal("ABC123", photo.Sha256);
    }

    [Fact]
    public void TryCreatePhoto_ReturnsFalse_ForInvalidSizeBytes()
    {
        var fields = new List<string>
        {
            "src","out","Images","2024","DateTaken",
            "2024-03-04T05:06:07.0000000Z",
            "",
            "2024-03-04T05:06:07.0000000Z",
            "2024-03-04T05:06:07.0000000Z",
            "NaN",".jpg","hash","false",""
        };

        var ok = PhotoRowMapper.TryCreatePhoto(fields, Indexes, out _, out var error);

        Assert.False(ok);
        Assert.Equal("invalid SizeBytes value.", error);
    }
}
