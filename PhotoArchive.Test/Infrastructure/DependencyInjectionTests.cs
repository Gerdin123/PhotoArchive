using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.Test.Infrastructure;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_UsesConfiguredConnectionString()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PhotoArchive"] = "Data Source=my-db.sqlite"
            })
            .Build();

        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<PhotoArchiveDbContext>();
        var connectionString = context.Database.GetConnectionString();

        Assert.Equal("Data Source=my-db.sqlite", connectionString);
    }

    [Fact]
    public void AddInfrastructure_BuildsFallbackConnectionString_FromDatabaseSettings()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:FolderPath"] = "C:\\archive",
                ["Database:FileName"] = "photos.db"
            })
            .Build();

        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<PhotoArchiveDbContext>();
        var connectionString = context.Database.GetConnectionString();

        Assert.Equal("Data Source=C:\\archive\\photos.db", connectionString);
    }

    [Fact]
    public void AddInfrastructure_UsesDefaultFileName_WhenNoDatabaseSettingsProvided()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<PhotoArchiveDbContext>();
        var connectionString = context.Database.GetConnectionString();

        Assert.Equal("Data Source=photoarchive.db", connectionString);
    }
}
