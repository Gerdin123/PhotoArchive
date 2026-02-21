using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace PhotoArchive.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PhotoArchive");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dbFolder = configuration["Database:FolderPath"] ?? string.Empty;
            var dbFileName = configuration["Database:FileName"] ?? "photoarchive.db";
            var dbPath = string.IsNullOrWhiteSpace(dbFolder)
                ? dbFileName
                : Path.Combine(dbFolder, dbFileName);

            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<PhotoArchiveDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}
