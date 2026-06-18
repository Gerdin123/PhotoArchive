using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class FileSystemScanner : IFileScanner
{
    public async IAsyncEnumerable<ScannedFile> ScanAsync(
        string inputRoot,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var files = Directory
            .EnumerateFiles(inputRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            yield return new ScannedFile(
                FullPath: info.FullName,
                OriginalFileName: info.Name,
                Extension: info.Extension,
                FileSizeBytes: info.Length);

            await Task.Yield();
        }
    }
}
