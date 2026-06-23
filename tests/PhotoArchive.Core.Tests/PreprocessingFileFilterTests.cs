using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Core.Tests;

public sealed class PreprocessingFileFilterTests
{
    [Theory]
    [InlineData("Thumbs.db", ".db")]
    [InlineData("clip.THM", ".THM")]
    [InlineData("THM_2812.jpg", ".jpg")]
    [InlineData("thm-2812.jpeg", ".jpeg")]
    [InlineData("thumb.jpg", ".jpg")]
    [InlineData("Thumb_preview.png", ".png")]
    [InlineData("thumbnail-small.webp", ".webp")]
    public void ShouldSkip_returns_true_for_thumbnail_and_system_artifacts(string fileName, string extension)
    {
        Assert.True(PreprocessingFileFilter.ShouldSkip(fileName, extension));
    }

    [Theory]
    [InlineData("IMG_0001.jpg", ".jpg")]
    [InlineData("family-thumbsup.jpg", ".jpg")]
    [InlineData("notes.txt", ".txt")]
    public void ShouldSkip_returns_false_for_normal_archive_files(string fileName, string extension)
    {
        Assert.False(PreprocessingFileFilter.ShouldSkip(fileName, extension));
    }
}
