using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using SkiaSharp;

namespace PhotoArchive.IntegrationTests;

public sealed class PhotoPreviewControlsTests
{
    [Fact]
    public async Task AvaloniaThumbnailService_reports_decoder_failures_without_null_reference()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-thumbnail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sourcePath = Path.Combine(root, "broken.jpg");
            var thumbnailPath = Path.Combine(root, "thumb.jpg");
            await File.WriteAllBytesAsync(sourcePath, new byte[] { 0xff, 0xd8, 0xff, 0x01 });

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                new AvaloniaThumbnailService().CreateThumbnailAsync(sourcePath, thumbnailPath));

            Assert.DoesNotContain("NullReferenceException", exception.ToString());
            Assert.Contains("decoder", exception.Message);
            Assert.False(File.Exists(thumbnailPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AvaloniaThumbnailService_generates_thumbnail_and_visual_analysis_for_valid_images()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-thumbnail-valid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sourcePath = Path.Combine(root, "source.png");
            var thumbnailPath = Path.Combine(root, "thumbnail.jpg");
            WriteTestImage(sourcePath);

            var analysis = await new AvaloniaThumbnailService().CreateThumbnailWithAnalysisAsync(sourcePath, thumbnailPath);

            Assert.Equal(thumbnailPath, analysis.ThumbnailPath);
            Assert.True(File.Exists(thumbnailPath));
            Assert.Matches("^#[0-9A-F]{6}$", analysis.AverageColorHex);
            Assert.Matches("^[0-9A-F]{16}$", analysis.PerceptualHash);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadPreviewBitmap_returns_null_for_supported_image_that_decoder_rejects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var path = Path.Combine(root, "broken.jpg");
            await File.WriteAllBytesAsync(path, new byte[] { 0xff, 0xd8, 0xff, 0x01 });
            var photo = new ReviewPhoto(
                Id: Guid.NewGuid(),
                OriginalPath: path,
                CurrentPath: path,
                OriginalFileName: "broken.jpg",
                MediaKind: MediaKind.SupportedImage,
                Status: ArchiveFileStatus.Processed,
                Sha256Hash: "hash",
                ThumbnailPath: null,
                InferredTakenDate: null,
                DateConfidence: DateConfidence.Unknown,
                Title: null,
                Tags: string.Empty);

            var bitmap = PhotoPreviewControls.LoadPreviewBitmap(photo, decodeWidth: 180);

            Assert.Null(bitmap);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WriteTestImage(string path)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality: 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
