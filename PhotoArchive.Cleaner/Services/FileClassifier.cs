using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    internal class FileClassifier : IFileClassifier
    {
        // This can be extended as you discover more formats in your archive.
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".dng", ".raw", ".cr2", ".nef", ".arw"
        };

        // Simple filename heuristics for generated previews/thumbnails.
        private static readonly string[] ThumbnailMarkers =
        [
            "thumb", "thumbnail", "preview", "thm"
        ];

        public FileType Classify(string filename)
        {
            var extension = Path.GetExtension(filename);
            if (!ImageExtensions.Contains(extension))
            {
                return FileType.Other;
            }

            // Even if extension is image-like, thumbnail files are routed to Others.
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            if (ThumbnailMarkers.Any(marker => fileNameWithoutExtension.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return FileType.Other;
            }

            return FileType.Image;
        }
    }
}
