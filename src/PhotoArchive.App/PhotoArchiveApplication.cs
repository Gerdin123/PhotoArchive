using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace PhotoArchive.App;

public sealed class PhotoArchiveApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = ResolveDatabasePath(desktop.Args ?? []);
            desktop.MainWindow = new Review.MainWindow(databasePath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string ResolveDatabasePath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Equals("--db", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                return Path.GetFullPath(args[i + 1]);
            }

            if (args[i].StartsWith("--db=", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(args[i]["--db=".Length..]);
            }
        }

        return Path.GetFullPath("photoarchive.db");
    }
}
