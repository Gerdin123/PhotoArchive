using Avalonia;

namespace PhotoArchive.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<PhotoArchiveApplication>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
