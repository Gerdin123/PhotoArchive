namespace PhotoArchive.Core.Preprocessing;

public interface IHashService
{
    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default);
}
