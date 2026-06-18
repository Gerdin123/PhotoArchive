using PhotoArchive.Infrastructure;

namespace PhotoArchive.IntegrationTests;

public sealed class FileSystemHashServiceTests
{
    [Fact]
    public async Task ComputeSha256Async_returns_stable_content_hash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"photoarchive-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "photo-archive");

        try
        {
            var hash = await new FileSystemHashService().ComputeSha256Async(path);

            Assert.Equal("ffc438b20b49a134b72d96f27e5a447f9521f062e9d257af54a07565775c80ae", hash);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
