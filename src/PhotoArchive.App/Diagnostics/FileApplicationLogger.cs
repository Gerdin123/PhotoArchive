using System.Globalization;

namespace PhotoArchive.App.Diagnostics;

public sealed class FileApplicationLogger : IApplicationLogger
{
    private readonly object gate = new();

    public FileApplicationLogger(string logDirectory)
    {
        LogDirectory = logDirectory;
    }

    public string LogDirectory { get; }

    public void Info(string source, string message)
    {
        Write("INF", source, message);
    }

    public void Warning(string source, string message)
    {
        Write("WRN", source, message);
    }

    public void Error(string source, string message, Exception? exception = null)
    {
        Write("ERR", source, exception is null ? message : $"{message} {exception}");
    }

    private void Write(string level, string source, string message)
    {
        Directory.CreateDirectory(LogDirectory);
        var path = Path.Combine(LogDirectory, $"photoarchive-{DateTime.UtcNow:yyyyMMdd}.log");
        var line = string.Join(
            " ",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            level,
            Escape(source),
            Escape(message));

        lock (gate)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
