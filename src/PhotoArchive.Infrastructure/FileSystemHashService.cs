using System.Security.Cryptography;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class FileSystemHashService : IHashService
{
    public async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
