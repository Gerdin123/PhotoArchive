using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class ThumbnailDetectorTests
{
    [Theory]
    [InlineData(@"C:\photos\thumb_image.jpg")]
    [InlineData(@"C:\photos\holiday\preview\img.jpg")]
    [InlineData(@"C:\photos\THUMBNAILS\img.jpg")]
    public void IsThumbnailFile_ReturnsTrue_ForKnownMarkers(string path)
    {
        Assert.True(ThumbnailDetector.IsThumbnailFile(path));
    }

    [Fact]
    public void IsThumbnailFile_ReturnsFalse_ForRegularFile()
    {
        Assert.False(ThumbnailDetector.IsThumbnailFile(@"C:\photos\2024\img.jpg"));
    }
}
