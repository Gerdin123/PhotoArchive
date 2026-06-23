using Avalonia;
using PhotoArchive.App.Diagnostics;

namespace PhotoArchive.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Current.Error("AppDomain", "Unhandled exception.", eventArgs.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Current.Error("TaskScheduler", "Unobserved task exception.", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            AppLog.Current.Info("Program", "Starting PhotoArchive.");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Current.Error("Program", "Fatal startup failure.", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<PhotoArchiveApplication>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
