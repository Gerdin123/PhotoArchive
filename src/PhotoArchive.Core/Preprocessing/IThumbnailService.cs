namespace PhotoArchive.Core.Preprocessing;

public interface IThumbnailService
{
    Task<string> CreateThumbnailAsync(string sourcePath, string outputPath, CancellationToken cancellationToken = default);
}
