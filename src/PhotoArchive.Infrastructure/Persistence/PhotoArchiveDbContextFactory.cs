using Microsoft.EntityFrameworkCore;

namespace PhotoArchive.Infrastructure.Persistence;

public static class PhotoArchiveDbContextFactory
{
    public static PhotoArchiveDbContext Create(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new DbContextOptionsBuilder<PhotoArchiveDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new PhotoArchiveDbContext(options);
    }
}
