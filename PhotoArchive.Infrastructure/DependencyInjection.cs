using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhotoArchive.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PhotoArchive")
            ?? "Data Source=photoarchive.db";

        services.AddDbContext<PhotoArchiveDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}
