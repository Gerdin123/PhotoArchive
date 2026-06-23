namespace PhotoArchive.App.Diagnostics;

public interface IApplicationLogger
{
    string LogDirectory { get; }
    void Info(string source, string message);
    void Warning(string source, string message);
    void Error(string source, string message, Exception? exception = null);
}
