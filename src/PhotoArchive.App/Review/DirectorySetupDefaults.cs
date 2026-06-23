namespace PhotoArchive.App.Review;

public static class DirectorySetupDefaults
{
    public const string DatabaseFileName = "photoarchive.db";

    public static DirectorySetupPaths FromInputRoot(string inputRoot)
    {
        var fullInputRoot = Path.GetFullPath(inputRoot);
        var cleanedRoot = Path.TrimEndingDirectorySeparator(fullInputRoot) + "cleaned";
        return new DirectorySetupPaths(
            InputRoot: fullInputRoot,
            OutputRoot: cleanedRoot,
            DatabasePath: Path.Combine(cleanedRoot, DatabaseFileName));
    }

    public static string GetFallbackDatabasePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Path.GetTempPath(), "PhotoArchive")
            : Path.Combine(appData, "PhotoArchive");

        return Path.Combine(root, DatabaseFileName);
    }
}

public sealed record DirectorySetupPaths(
    string InputRoot,
    string OutputRoot,
    string DatabasePath);
