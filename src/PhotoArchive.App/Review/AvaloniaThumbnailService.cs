using PhotoArchive.Core.Preprocessing;
using SkiaSharp;

namespace PhotoArchive.App.Review;

public sealed record ThumbnailAnalysis(string ThumbnailPath, string AverageColorHex, string PerceptualHash);

public sealed class AvaloniaThumbnailService : IThumbnailService
{
    private const int ThumbnailMaxEdge = 480;
    private const int AnalysisSize = 8;

    public async Task<string> CreateThumbnailAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var analysis = await CreateThumbnailWithAnalysisAsync(sourcePath, outputPath, cancellationToken);
        return analysis.ThumbnailPath;
    }

    public Task<ThumbnailAnalysis> CreateThumbnailWithAnalysisAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source image does not exist.", sourcePath);
        }

        using var source = SKBitmap.Decode(sourcePath)
            ?? throw new NotSupportedException("The image decoder could not read this file.");
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new NotSupportedException("The image has invalid dimensions.");
        }

        var thumbnailSize = CalculateThumbnailSize(source.Width, source.Height, ThumbnailMaxEdge);
        using var thumbnail = Resize(source, thumbnailSize.Width, thumbnailSize.Height);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var image = SKImage.FromBitmap(thumbnail);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality: 82)
            ?? throw new InvalidOperationException("The thumbnail encoder failed.");
        using (var stream = File.Create(outputPath))
        {
            data.SaveTo(stream);
        }

        var averageColorHex = CalculateAverageColorHex(source);
        var perceptualHash = CalculateAverageHash(source);
        return Task.FromResult(new ThumbnailAnalysis(outputPath, averageColorHex, perceptualHash));
    }

    private static (int Width, int Height) CalculateThumbnailSize(int width, int height, int maxEdge)
    {
        var scale = Math.Min((double)maxEdge / width, (double)maxEdge / height);
        if (scale >= 1)
        {
            return (width, height);
        }

        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
        {
            return source.Copy();
        }

        var resized = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(resized);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawBitmap(source, new SKRect(0, 0, width, height), paint);
        canvas.Flush();
        return resized;
    }

    private static string CalculateAverageColorHex(SKBitmap source)
    {
        using var sample = Resize(source, Math.Min(32, source.Width), Math.Min(32, source.Height));
        long red = 0;
        long green = 0;
        long blue = 0;
        var count = sample.Width * sample.Height;
        for (var y = 0; y < sample.Height; y++)
        {
            for (var x = 0; x < sample.Width; x++)
            {
                var pixel = sample.GetPixel(x, y);
                red += pixel.Red;
                green += pixel.Green;
                blue += pixel.Blue;
            }
        }

        return $"#{red / count:X2}{green / count:X2}{blue / count:X2}";
    }

    private static string CalculateAverageHash(SKBitmap source)
    {
        using var sample = Resize(source, AnalysisSize, AnalysisSize);
        var values = new byte[AnalysisSize * AnalysisSize];
        var total = 0;
        var index = 0;
        for (var y = 0; y < AnalysisSize; y++)
        {
            for (var x = 0; x < AnalysisSize; x++)
            {
                var pixel = sample.GetPixel(x, y);
                var gray = (byte)((pixel.Red * 299 + pixel.Green * 587 + pixel.Blue * 114) / 1000);
                values[index++] = gray;
                total += gray;
            }
        }

        var average = total / values.Length;
        ulong hash = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] >= average)
            {
                hash |= 1UL << i;
            }
        }

        return hash.ToString("X16");
    }
}
