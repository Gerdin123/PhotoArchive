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
    public async Task Review_repository_filters_by_all_selected_tags_and_lists_available_years_and_decades()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-tags-all-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var bothId = Guid.NewGuid();
            var personOnlyId = Guid.NewGuid();
            var otherDecadeId = Guid.NewGuid();
            Guid personTagId;
            Guid placeTagId;
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                AddPhoto(dbContext, bothId, root, "both.jpg", ArchiveFileStatus.Processed, "hash-both", new DateTimeOffset(2001, 5, 6, 8, 0, 0, TimeSpan.Zero));
                AddPhoto(dbContext, personOnlyId, root, "person-only.jpg", ArchiveFileStatus.Processed, "hash-person", new DateTimeOffset(2002, 5, 6, 8, 0, 0, TimeSpan.Zero));
                AddPhoto(dbContext, otherDecadeId, root, "other-decade.jpg", ArchiveFileStatus.Processed, "hash-other-decade", new DateTimeOffset(2014, 5, 6, 8, 0, 0, TimeSpan.Zero));

                var person = new Tag { Name = "Person 1", Type = TagType.Person };
                var place = new Tag { Name = "Place 1", Type = TagType.Place };
                dbContext.Tags.AddRange(person, place);
                dbContext.PhotoTags.AddRange(
                    new PhotoTag { ArchiveFileId = bothId, TagId = person.Id },
                    new PhotoTag { ArchiveFileId = bothId, TagId = place.Id },
                    new PhotoTag { ArchiveFileId = personOnlyId, TagId = person.Id });
                await dbContext.SaveChangesAsync();
                personTagId = person.Id;
                placeTagId = place.Id;
            }

            var repository = new PhotoReviewRepository(databasePath);
            var tagged = await repository.GetPhotosAsync(new ReviewFilter(TagIds: [personTagId, placeTagId]));
            var years = await repository.GetAvailableYearsAsync();
            var decades = await repository.GetAvailableDecadesAsync();

            Assert.Single(tagged);
            Assert.Equal(bothId, tagged[0].Id);
            Assert.Equal([2001, 2002, 2014], years);
            Assert.Equal([2000, 2010], decades);
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
            var visualRelatedId = Guid.NewGuid();
            var unrelatedId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                AddPhoto(dbContext, targetId, root, "batch\\target.jpg", ArchiveFileStatus.Processed, "hash-target", new DateTimeOffset(2014, 1, 2, 8, 0, 0, TimeSpan.Zero), "Canon", 4000, 3000, "#445566", "1010101010101010");
                AddPhoto(dbContext, relatedId, root, "batch\\related.jpg", ArchiveFileStatus.Processed, "hash-related", new DateTimeOffset(2014, 1, 2, 9, 0, 0, TimeSpan.Zero), "Canon", 4000, 3000, "#445566", "1010101010101010");
                AddPhoto(dbContext, visualRelatedId, root, "other\\visual.jpg", ArchiveFileStatus.Processed, "hash-visual", new DateTimeOffset(2022, 6, 7, 9, 0, 0, TimeSpan.Zero), "Nikon", 1000, 800, "#465568", "1010101010101110");
                AddPhoto(dbContext, unrelatedId, root, "other\\unrelated.jpg", ArchiveFileStatus.Processed, "hash-other", new DateTimeOffset(2022, 6, 7, 9, 0, 0, TimeSpan.Zero), "Nikon", 1000, 800, "#ffffff", "0101010101010101");

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
            Assert.Contains("same visual hash", related.Reasons);
            Assert.Contains("similar average color", related.Reasons);
            var visualRelated = Assert.Single(details.RelatedPhotos, item => item.Photo.Id == visualRelatedId);
            Assert.Contains("similar visual hash", visualRelated.Reasons);
            Assert.Contains("similar average color", visualRelated.Reasons);
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
        int height = 1000,
        string? averageColorHex = null,
        string? perceptualHash = null)
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
            Height = height,
            AverageColorHex = averageColorHex,
            PerceptualHash = perceptualHash
        });
    }
}
