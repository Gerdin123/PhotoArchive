using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class SearchAndRelatedReviewTests
{
    [Fact]
    public async Task Review_repository_filters_by_search_status_and_date_range()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-search-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var wantedId = Guid.NewGuid();
            var hiddenId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                AddPhoto(dbContext, wantedId, root, "Paris_20140102.jpg", ArchiveFileStatus.Processed, "hash-1", new DateTimeOffset(2014, 1, 2, 8, 0, 0, TimeSpan.Zero));
                AddPhoto(dbContext, hiddenId, root, "Paris_20200102.jpg", ArchiveFileStatus.Deleted, "hash-2", new DateTimeOffset(2020, 1, 2, 8, 0, 0, TimeSpan.Zero));
                await dbContext.SaveChangesAsync();
            }

            var repository = new PhotoReviewRepository(databasePath);
            var results = await repository.GetPhotosAsync(new ReviewFilter(
                SearchText: "Paris",
                Status: ArchiveFileStatus.Processed,
                From: new DateTimeOffset(2014, 1, 1, 0, 0, 0, TimeSpan.Zero),
                To: new DateTimeOffset(2014, 12, 31, 23, 59, 59, TimeSpan.Zero)));

            Assert.Single(results);
            Assert.Equal(wantedId, results[0].Id);
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
    public async Task Details_include_related_photos_with_reason_codes()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-related-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var targetId = Guid.NewGuid();
            var relatedId = Guid.NewGuid();
            var unrelatedId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                AddPhoto(dbContext, targetId, root, "batch\\target.jpg", ArchiveFileStatus.Processed, "hash-target", new DateTimeOffset(2014, 1, 2, 8, 0, 0, TimeSpan.Zero), "Canon", 4000, 3000);
                AddPhoto(dbContext, relatedId, root, "batch\\related.jpg", ArchiveFileStatus.Processed, "hash-related", new DateTimeOffset(2014, 1, 2, 9, 0, 0, TimeSpan.Zero), "Canon", 4000, 3000);
                AddPhoto(dbContext, unrelatedId, root, "other\\unrelated.jpg", ArchiveFileStatus.Processed, "hash-other", new DateTimeOffset(2022, 6, 7, 9, 0, 0, TimeSpan.Zero), "Nikon", 1000, 800);

                var tag = new Tag { Name = "Trip", Type = TagType.Event };
                dbContext.Tags.Add(tag);
                dbContext.PhotoTags.Add(new PhotoTag { ArchiveFileId = targetId, TagId = tag.Id });
                dbContext.PhotoTags.Add(new PhotoTag { ArchiveFileId = relatedId, TagId = tag.Id });
                await dbContext.SaveChangesAsync();
            }

            var details = await new PhotoReviewRepository(databasePath).GetDetailsAsync(targetId);

            var related = Assert.Single(details!.RelatedPhotos, item => item.Photo.Id == relatedId);
            Assert.Contains("same day", related.Reasons);
            Assert.Contains("same source folder", related.Reasons);
            Assert.Contains("same camera", related.Reasons);
            Assert.Contains("same dimensions", related.Reasons);
            Assert.Contains("shared tag", related.Reasons);
            Assert.DoesNotContain(details.RelatedPhotos, item => item.Photo.Id == unrelatedId);
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

    private static void AddPhoto(
        PhotoArchiveDbContext dbContext,
        Guid id,
        string root,
        string relativePath,
        ArchiveFileStatus status,
        string hash,
        DateTimeOffset date,
        string cameraModel = "Camera",
        int width = 2000,
        int height = 1000)
    {
        var originalPath = Path.Combine(root, relativePath);
        dbContext.ArchiveFiles.Add(new ArchiveFile
        {
            Id = id,
            OriginalPath = originalPath,
            CurrentPath = originalPath,
            OriginalFileName = Path.GetFileName(originalPath),
            Extension = ".jpg",
            FileSizeBytes = 1,
            Sha256Hash = hash,
            MediaKind = MediaKind.SupportedImage,
            Status = status
        });

        dbContext.PhotoMetadata.Add(new PhotoMetadata
        {
            ArchiveFileId = id,
            InferredTakenDate = date,
            DateConfidence = DateConfidence.High,
            CameraModel = cameraModel,
            Width = width,
            Height = height
        });
    }
}
