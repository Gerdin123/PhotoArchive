using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using PhotoArchive.App.Diagnostics;
using PhotoArchive.App.Review;

namespace PhotoArchive.App;

public sealed class PhotoArchiveApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        Styles.Add(new Style(selector => selector.OfType<TextBlock>())
        {
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)))
            }
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databasePath = ResolveDatabasePath(desktop.Args ?? []);
            AppLog.Current.Info("PhotoArchiveApplication", $"Opening main window with database '{databasePath}'. Logs: '{AppLog.Current.LogDirectory}'.");
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

        return DirectorySetupDefaults.GetFallbackDatabasePath();
    }
}
