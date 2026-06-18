using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task DbContext_creates_sqlite_schema_for_archive_entities()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"photoarchive-{Guid.NewGuid():N}.db");

        try
        {
            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            await dbContext.Database.MigrateAsync();

            dbContext.Tags.Add(new Tag { Name = "Family", Type = TagType.Event });
            await dbContext.SaveChangesAsync();

            Assert.Equal(1, await dbContext.Tags.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task PreprocessingPlanImporter_stores_files_metadata_duplicates_and_operations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-db-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);

        try
        {
            var canonicalPath = Path.Combine(input, "canonical.jpg");
            var duplicatePath = Path.Combine(input, "duplicate.jpg");
            await File.WriteAllTextAsync(canonicalPath, "same");
            await File.WriteAllTextAsync(duplicatePath, "same");

            var takenDate = new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero);
            var operations = new[]
            {
                new PlannedFileOperation(
                    SourcePath: canonicalPath,
                    DestinationPath: Path.Combine(root, "output", "Photos", "2010-2019", "2010", "20100102 - 1.jpg"),
                    MediaKind: MediaKind.SupportedImage,
                    Sha256Hash: "same-hash",
                    InferredTakenDate: takenDate,
                    DateConfidence: DateConfidence.High,
                    DateSource: "EXIF:DateTimeOriginal",
                    IsDuplicate: false,
                    CanonicalSourcePath: null,
                    DuplicateGroupId: null,
                    ExecutionResult: "Copied"),
                new PlannedFileOperation(
                    SourcePath: duplicatePath,
                    DestinationPath: Path.Combine(root, "output", "Duplicates", "2010-2019", "2010", "duplicate.jpg"),
                    MediaKind: MediaKind.Duplicate,
                    Sha256Hash: "same-hash",
                    InferredTakenDate: takenDate,
                    DateConfidence: DateConfidence.High,
                    DateSource: "EXIF:DateTimeOriginal",
                    IsDuplicate: true,
                    CanonicalSourcePath: canonicalPath,
                    DuplicateGroupId: "same-hash",
                    ExecutionResult: "Copied")
            };

            var plan = new OutputPlan(
                new PreprocessingSettings(input, Path.Combine(root, "output"), Execute: true),
                new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
                operations);

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            await dbContext.Database.MigrateAsync();
            var result = await new PreprocessingPlanImporter().ImportAsync(dbContext, plan);

            Assert.Equal(2, result.ArchiveFiles);
            Assert.Equal(2, result.MetadataRows);
            Assert.Equal(1, result.DuplicateGroups);
            Assert.Equal(2, result.OperationLogs);
            Assert.Equal(2, await dbContext.ArchiveFiles.CountAsync());
            Assert.Equal(2, await dbContext.PhotoMetadata.CountAsync());
            Assert.Equal(1, await dbContext.DuplicateGroups.CountAsync());
            Assert.Equal(2, await dbContext.OperationLogs.CountAsync());
            Assert.Equal(ArchiveFileStatus.Duplicate, await dbContext.ArchiveFiles
                .Where(file => file.OriginalPath == duplicatePath)
                .Select(file => file.Status)
                .SingleAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreprocessingPlanImporter_can_import_same_plan_more_than_once()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-repeat-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);

        try
        {
            var sourcePath = Path.Combine(input, "IMG_20100102.jpg");
            await File.WriteAllTextAsync(sourcePath, "photo");

            var date = new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero);
            var plan = new OutputPlan(
                new PreprocessingSettings(input, Path.Combine(root, "output"), Execute: false),
                new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
                new[]
                {
                    new PlannedFileOperation(
                        SourcePath: sourcePath,
                        DestinationPath: Path.Combine(root, "output", "Photos", "2010-2019", "2010", "20100102 - 1.jpg"),
                        MediaKind: MediaKind.SupportedImage,
                        Sha256Hash: "hash",
                        InferredTakenDate: date,
                        DateConfidence: DateConfidence.High,
                        DateSource: "EXIF:DateTimeOriginal",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null)
                });

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            await dbContext.Database.MigrateAsync();
            var importer = new PreprocessingPlanImporter();

            await importer.ImportAsync(dbContext, plan);
            await importer.ImportAsync(dbContext, plan);

            Assert.Equal(1, await dbContext.ArchiveFiles.CountAsync());
            Assert.Equal(1, await dbContext.PhotoMetadata.CountAsync());
            Assert.Equal(2, await dbContext.OperationLogs.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
