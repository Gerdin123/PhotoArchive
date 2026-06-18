using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoArchive.Infrastructure.Persistence;

public sealed class PhotoArchiveDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhotoArchiveDbContext>
{
    public PhotoArchiveDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.FirstOrDefault(arg => arg.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            ?? "photoarchive.design.db";

        var options = new DbContextOptionsBuilder<PhotoArchiveDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new PhotoArchiveDbContext(options);
    }
}
