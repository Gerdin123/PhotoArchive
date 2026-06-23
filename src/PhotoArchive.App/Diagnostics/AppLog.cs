namespace PhotoArchive.App.Diagnostics;

public static class AppLog
{
    public static IApplicationLogger Current { get; set; } =
        new FileApplicationLogger(GetDefaultLogDirectory());

    public static string GetDefaultLogDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Path.GetTempPath(), "PhotoArchive")
            : Path.Combine(appData, "PhotoArchive");

        return Path.Combine(root, "Logs");
    }
}
