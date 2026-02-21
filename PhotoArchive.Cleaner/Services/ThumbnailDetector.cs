namespace PhotoArchive.Cleaner.Services;

internal static class ThumbnailDetector
{
    private static readonly string[] ThumbnailMarkers =
    [
        "thumb", "thumbnail", "preview", "thm"
    ];

    public static bool IsThumbnailFile(string filePath)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (ThumbnailMarkers.Any(marker => fileNameWithoutExtension.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var segments = directoryPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => ThumbnailMarkers.Any(marker => segment.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }
}
