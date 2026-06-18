namespace PhotoArchive.Core.Preprocessing;

public interface IFileScanner
{
    IAsyncEnumerable<ScannedFile> ScanAsync(string inputRoot, CancellationToken cancellationToken = default);
}
