using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class FileClassifierTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    [InlineData("folder/image.heic")]
    public void Classify_ReturnsImage_ForSupportedImageExtension(string fileName)
    {
        var classifier = new FileClassifier();

        var result = classifier.Classify(fileName);

        Assert.Equal(FileType.Image, result);
    }

    [Theory]
    [InlineData("document.txt")]
    [InlineData("archive.zip")]
    public void Classify_ReturnsOther_ForNonImageExtension(string fileName)
    {
        var classifier = new FileClassifier();

        var result = classifier.Classify(fileName);

        Assert.Equal(FileType.Other, result);
    }

    [Theory]
    [InlineData("thumb.jpg")]
    [InlineData("Vacation_THUMBNAIL.png")]
    [InlineData("my-preview.webp")]
    public void Classify_ReturnsOther_ForThumbnailLikeNames(string fileName)
    {
        var classifier = new FileClassifier();

        var result = classifier.Classify(fileName);

        Assert.Equal(FileType.Other, result);
    }
}
