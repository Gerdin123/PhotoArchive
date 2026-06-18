using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class ReviewRepositoryEdgeCaseTests
{
    [Fact]
    public async Task GetPhotoPageAsync_returns_empty_page_with_safe_page_values()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-empty-page-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
            }

            var page = await new PhotoReviewRepository(databasePath).GetPhotoPageAsync(new ReviewFilter(), pageNumber: -4, pageSize: 0);

            Assert.Empty(page.Photos);
            Assert.Equal(1, page.PageNumber);
            Assert.Equal(1, page.PageSize);
            Assert.Equal(0, page.TotalCount);
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
    public async Task AddTagAsync_reuses_existing_tag_and_does_not_duplicate_photo_tag()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-tags-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var fileId = Guid.NewGuid();
            await SeedPhotoAsync(databasePath, root, fileId, "photo.jpg", ArchiveFileStatus.Processed, DateConfidence.High);

            var repository = new PhotoReviewRepository(databasePath);
            var first = await repository.AddTagAsync(fileId, "  Vacation  ", TagType.Event);
            var second = await repository.AddTagAsync(fileId, "Vacation", TagType.Event);

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, await dbContext.Tags.CountAsync());
            Assert.Equal(1, await dbContext.PhotoTags.CountAsync());
            Assert.Equal(1, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "TagAdded"));
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
    public async Task RemoveTagAsync_removes_existing_link_and_ignores_missing_link()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-remove-tag-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var fileId = Guid.NewGuid();
            await SeedPhotoAsync(databasePath, root, fileId, "photo.jpg", ArchiveFileStatus.Processed, DateConfidence.High);

            var repository = new PhotoReviewRepository(databasePath);
            var tag = await repository.AddTagAsync(fileId, "Family", TagType.Event);
            await repository.RemoveTagAsync(fileId, tag.Id);
            await repository.RemoveTagAsync(fileId, tag.Id);

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            Assert.Equal(0, await dbContext.PhotoTags.CountAsync());
            Assert.Equal(1, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "TagRemoved"));
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
    public async Task CorrectTakenDateAsync_creates_metadata_row_when_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-correct-missing-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var fileId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(CreateArchiveFile(fileId, root, "missing-metadata.jpg", ArchiveFileStatus.NeedsReview));
                await dbContext.SaveChangesAsync();
            }

            var correctedDate = new DateTimeOffset(2005, 4, 3, 2, 1, 0, TimeSpan.Zero);
            await new PhotoReviewRepository(databasePath).CorrectTakenDateAsync(fileId, correctedDate, "");

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var metadata = await verification.PhotoMetadata.SingleAsync(row => row.ArchiveFileId == fileId);
            Assert.Equal(correctedDate, metadata.InferredTakenDate);
            Assert.Equal(DateConfidence.High, metadata.DateConfidence);
            Assert.Equal("Manual review correction", await verification.ManualCorrections.Select(correction => correction.Reason).SingleAsync());
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
    public async Task MarkDuplicateAsync_throws_when_duplicate_or_canonical_is_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-dup-missing-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var existingId = Guid.NewGuid();
            await SeedPhotoAsync(databasePath, root, existingId, "photo.jpg", ArchiveFileStatus.Processed, DateConfidence.High);
            var repository = new PhotoReviewRepository(databasePath);

            var missingDuplicate = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.MarkDuplicateAsync(Guid.NewGuid(), existingId));
            var missingCanonical = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.MarkDuplicateAsync(existingId, Guid.NewGuid()));

            Assert.Contains("Duplicate file was not found", missingDuplicate.Message);
            Assert.Contains("Canonical file was not found", missingCanonical.Message);
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

    [Theory]
    [InlineData(ReviewSortMode.FileName, new[] { "alpha.jpg", "middle.jpg", "zulu.jpg" })]
    [InlineData(ReviewSortMode.Status, new[] { "alpha.jpg", "zulu.jpg", "middle.jpg" })]
    [InlineData(ReviewSortMode.DateConfidence, new[] { "alpha.jpg", "middle.jpg", "zulu.jpg" })]
    public async Task GetPhotoPageAsync_sorts_by_file_name_status_and_confidence(
        ReviewSortMode sortMode,
        string[] expectedNames)
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-sort-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            await SeedPhotoAsync(databasePath, root, Guid.NewGuid(), "zulu.jpg", ArchiveFileStatus.NeedsReview, DateConfidence.Unknown);
            await SeedPhotoAsync(databasePath, root, Guid.NewGuid(), "alpha.jpg", ArchiveFileStatus.Processed, DateConfidence.High);
            await SeedPhotoAsync(databasePath, root, Guid.NewGuid(), "middle.jpg", ArchiveFileStatus.Duplicate, DateConfidence.Low);

            var page = await new PhotoReviewRepository(databasePath).GetPhotoPageAsync(new ReviewFilter(SortMode: sortMode), 1, 10);

            Assert.Equal(expectedNames, page.Photos.Select(photo => photo.OriginalFileName).ToArray());
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
    public async Task GetDetailsAsync_returns_null_for_missing_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-details-missing-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
            }

            var details = await new PhotoReviewRepository(databasePath).GetDetailsAsync(Guid.NewGuid());

            Assert.Null(details);
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

    private static async Task SeedPhotoAsync(
        string databasePath,
        string root,
        Guid fileId,
        string fileName,
        ArchiveFileStatus status,
        DateConfidence confidence)
    {
        await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
        await dbContext.Database.MigrateAsync();
        dbContext.ArchiveFiles.Add(CreateArchiveFile(fileId, root, fileName, status));
        dbContext.PhotoMetadata.Add(new PhotoMetadata
        {
            ArchiveFileId = fileId,
            InferredTakenDate = new DateTimeOffset(2020, 1, 2, 3, 4, 0, TimeSpan.Zero),
            DateConfidence = confidence
        });
        await dbContext.SaveChangesAsync();
    }

    private static ArchiveFile CreateArchiveFile(
        Guid fileId,
        string root,
        string fileName,
        ArchiveFileStatus status)
    {
        var path = Path.Combine(root, fileName);
        return new ArchiveFile
        {
            Id = fileId,
            OriginalPath = path,
            CurrentPath = path,
            OriginalFileName = fileName,
            Extension = Path.GetExtension(fileName),
            FileSizeBytes = 1,
            Sha256Hash = fileId.ToString("N"),
            MediaKind = MediaKind.SupportedImage,
            Status = status
        };
    }
}
