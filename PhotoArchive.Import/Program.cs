using Microsoft.EntityFrameworkCore;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.Import;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (!ImportOptionsResolver.TryResolve(args, out var options))
        {
            return 1;
        }

        var dbOptions = new DbContextOptionsBuilder<PhotoArchiveDbContext>()
            .UseSqlite($"Data Source={options.DatabasePath}")
            .Options;

        using var dbContext = new PhotoArchiveDbContext(dbOptions);
        dbContext.Database.EnsureCreated();

        var importer = new ManifestImportService();
        var summary = importer.ImportManifest(dbContext, options.ManifestPath);

        Console.WriteLine();
        Console.WriteLine($"Manifest: {options.ManifestPath}");
        Console.WriteLine($"Database: {options.DatabasePath}");
        Console.WriteLine($"Imported photos: {summary.Imported}");
        Console.WriteLine($"Skipped (non-images/duplicates): {summary.SkippedFiltered}");
        Console.WriteLine($"Skipped (duplicate hash within import): {summary.SkippedDuplicateHash}");
        Console.WriteLine($"Skipped (invalid rows): {summary.SkippedInvalid}");

        return 0;
    }
}
