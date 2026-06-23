namespace PhotoArchive.Core.Preprocessing;

public static class PreprocessingFileFilter
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".thm"
    };

    private static readonly HashSet<string> IgnoredFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "thumbs.db"
    };

    public static bool ShouldSkip(ScannedFile file)
    {
        return ShouldSkip(file.OriginalFileName, file.Extension);
    }

    public static bool ShouldSkip(string fileName, string extension)
    {
        if (IgnoredExtensions.Contains(extension))
        {
            return true;
        }

        if (IgnoredFileNames.Contains(fileName))
        {
            return true;
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return nameWithoutExtension.Equals("thumb", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.Equals("thm", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thumb_", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thumb-", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thm_", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thm-", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thumbnail_", StringComparison.OrdinalIgnoreCase)
            || nameWithoutExtension.StartsWith("thumbnail-", StringComparison.OrdinalIgnoreCase);
    }
}
