using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class ReviewRepositoryTests
{
    [Fact]
    public async Task Review_repository_supports_date_tags_duplicates_and_hide_workflows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-review-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var canonicalId = Guid.NewGuid();
            var duplicateId = Guid.NewGuid();
            await SeedDatabaseAsync(databasePath, canonicalId, duplicateId);

            var repository = new PhotoReviewRepository(databasePath);
            await repository.InitializeAsync();

            var photos = await repository.GetPhotosAsync(new ReviewFilter());
            Assert.Equal(2, photos.Count);

            var correctedDate = new DateTimeOffset(2011, 3, 4, 5, 6, 0, TimeSpan.Zero);
            await repository.CorrectTakenDateAsync(canonicalId, correctedDate, "Scanned album note");
            await repository.AddTagAsync(canonicalId, "Vacation", TagType.Event);
            await repository.MarkDuplicateAsync(duplicateId, canonicalId);
            await repository.HideAsync(duplicateId);

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            var metadata = await dbContext.PhotoMetadata.SingleAsync(row => row.ArchiveFileId == canonicalId);
            Assert.Equal(correctedDate, metadata.InferredTakenDate);
            Assert.Equal(DateConfidence.High, metadata.DateConfidence);
            Assert.Equal(1, await dbContext.ManualCorrections.CountAsync());
            Assert.Equal(1, await dbContext.PhotoTags.CountAsync());
            Assert.Equal(1, await dbContext.DuplicateGroups.CountAsync());
            Assert.Equal(ArchiveFileStatus.Deleted, await dbContext.ArchiveFiles
                .Where(file => file.Id == duplicateId)
                .Select(file => file.Status)
                .SingleAsync());
            Assert.True(await dbContext.OperationLogs.CountAsync() >= 4);
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
    public async Task Review_repository_filters_by_tag_and_duplicate_state()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-review-filter-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var canonicalId = Guid.NewGuid();
            var duplicateId = Guid.NewGuid();
            await SeedDatabaseAsync(databasePath, canonicalId, duplicateId);

            var repository = new PhotoReviewRepository(databasePath);
            await repository.InitializeAsync();
            var tag = await repository.AddTagAsync(canonicalId, "Family", TagType.Event);
            await repository.MarkDuplicateAsync(duplicateId, canonicalId);

            var tagged = await repository.GetPhotosAsync(new ReviewFilter(TagId: tag.Id));
            var duplicates = await repository.GetPhotosAsync(new ReviewFilter(DuplicatesOnly: true));

            Assert.Single(tagged);
            Assert.Equal(canonicalId, tagged[0].Id);
            Assert.Single(duplicates);
            Assert.Equal(duplicateId, duplicates[0].Id);
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

    private static async Task SeedDatabaseAsync(string databasePath, Guid canonicalId, Guid duplicateId)
    {
        await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
        await dbContext.Database.MigrateAsync();

        var date = new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero);
        dbContext.ArchiveFiles.AddRange(
            new ArchiveFile
            {
                Id = canonicalId,
                OriginalPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "canonical.jpg"),
                CurrentPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "archive", "canonical.jpg"),
                OriginalFileName = "canonical.jpg",
                Extension = ".jpg",
                FileSizeBytes = 10,
                Sha256Hash = "same-hash",
                MediaKind = MediaKind.SupportedImage,
                Status = ArchiveFileStatus.Processed
            },
            new ArchiveFile
            {
                Id = duplicateId,
                OriginalPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "duplicate.jpg"),
                CurrentPath = Path.Combine(Path.GetDirectoryName(databasePath)!, "archive", "duplicate.jpg"),
                OriginalFileName = "duplicate.jpg",
                Extension = ".jpg",
                FileSizeBytes = 10,
                Sha256Hash = "same-hash",
                MediaKind = MediaKind.SupportedImage,
                Status = ArchiveFileStatus.Processed
            });

        dbContext.PhotoMetadata.AddRange(
            new PhotoMetadata
            {
                ArchiveFileId = canonicalId,
                InferredTakenDate = date,
                DateConfidence = DateConfidence.High
            },
            new PhotoMetadata
            {
                ArchiveFileId = duplicateId,
                InferredTakenDate = date.AddMinutes(1),
                DateConfidence = DateConfidence.High
            });

        await dbContext.SaveChangesAsync();
    }
}
