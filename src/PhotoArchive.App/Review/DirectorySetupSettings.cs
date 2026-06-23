using System.Text.Json;

namespace PhotoArchive.App.Review;

public sealed record DirectorySetupSettings(
    string? InputRoot,
    string? OutputRoot,
    string? DatabasePath);

public sealed class DirectorySetupSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string settingsPath;

    public DirectorySetupSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public DirectorySetupSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new DirectorySetupSettings(null, null, null);
        }

        try
        {
            return JsonSerializer.Deserialize<DirectorySetupSettings>(File.ReadAllText(settingsPath), JsonOptions)
                ?? new DirectorySetupSettings(null, null, null);
        }
        catch (JsonException)
        {
            return new DirectorySetupSettings(null, null, null);
        }
        catch (IOException)
        {
            return new DirectorySetupSettings(null, null, null);
        }
    }

    public void Save(DirectorySetupSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Path.GetTempPath(), "PhotoArchive")
            : Path.Combine(appData, "PhotoArchive");

        return Path.Combine(root, "settings.json");
    }
}
