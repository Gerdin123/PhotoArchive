using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class SimpleFileClassifier : IFileClassifier
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",
        ".heic",
        ".heif"
    };

    public async Task<MediaClassification> ClassifyAsync(
        ScannedFile file,
        CancellationToken cancellationToken = default)
    {
        if (!SupportedImageExtensions.Contains(file.Extension))
        {
            return new MediaClassification(MediaKind.Unsupported, "Unsupported extension.");
        }

        var signature = await ReadSignatureAsync(file.FullPath, cancellationToken);
        if (SignatureMatches(file.Extension, signature))
        {
            return new MediaClassification(MediaKind.SupportedImage, "Supported extension and signature.");
        }

        if (file.Extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
            || file.Extension.Equals(".heif", StringComparison.OrdinalIgnoreCase))
        {
            return new MediaClassification(MediaKind.SupportedImage, "Supported HEIF extension.");
        }

        return new MediaClassification(MediaKind.SupportedImage, "Supported extension; signature not recognized.");
    }

    private static async Task<byte[]> ReadSignatureAsync(string path, CancellationToken cancellationToken)
    {
        var buffer = new byte[16];
        await using var stream = File.OpenRead(path);
        var read = await stream.ReadAsync(buffer, cancellationToken);
        return buffer[..read];
    }

    private static bool SignatureMatches(string extension, ReadOnlySpan<byte> signature)
    {
        if ((extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            && signature.Length >= 3)
        {
            return signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF;
        }

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && signature.Length >= 8)
        {
            return signature[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        }

        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) && signature.Length >= 6)
        {
            return signature[..6].SequenceEqual("GIF87a"u8) || signature[..6].SequenceEqual("GIF89a"u8);
        }

        if (extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) && signature.Length >= 2)
        {
            return signature[0] == 0x42 && signature[1] == 0x4D;
        }

        if ((extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
            && signature.Length >= 4)
        {
            return signature[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 })
                || signature[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A });
        }

        if (extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) && signature.Length >= 12)
        {
            return signature[..4].SequenceEqual("RIFF"u8) && signature[8..12].SequenceEqual("WEBP"u8);
        }

        return false;
    }
}
