using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.IntegrationTests;

public sealed class ProjectReferenceTests
{
    [Fact]
    public void Infrastructure_hash_service_implements_core_contract()
    {
        Assert.IsAssignableFrom<IHashService>(new FileSystemHashService());
    }
}
