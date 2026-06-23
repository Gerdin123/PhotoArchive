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
    public async Task CorrectTakenDateAsync_resequences_old_and_new_day_current_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-resequence-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var movedId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                var moved = CreateArchivedPhoto(movedId, root, "moved.jpg", "hash-moved", new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero), 1);
                var oldDayStays = CreateArchivedPhoto(Guid.NewGuid(), root, "old-day-stays.jpg", "hash-old", new DateTimeOffset(2010, 1, 2, 9, 0, 0, TimeSpan.Zero), 2);
                var newDayFirst = CreateArchivedPhoto(Guid.NewGuid(), root, "new-day-first.jpg", "hash-first", new DateTimeOffset(2010, 1, 3, 7, 0, 0, TimeSpan.Zero), 1);
                var newDayLast = CreateArchivedPhoto(Guid.NewGuid(), root, "new-day-last.jpg", "hash-last", new DateTimeOffset(2010, 1, 3, 10, 0, 0, TimeSpan.Zero), 2);
                dbContext.ArchiveFiles.AddRange(
                    moved.File,
                    oldDayStays.File,
                    newDayFirst.File,
                    newDayLast.File);
                dbContext.PhotoMetadata.AddRange(
                    moved.Metadata,
                    oldDayStays.Metadata,
                    newDayFirst.Metadata,
                    newDayLast.Metadata);
                await dbContext.SaveChangesAsync();
            }

            var correctedDate = new DateTimeOffset(2010, 1, 3, 8, 30, 0, TimeSpan.Zero);
            await new PhotoReviewRepository(databasePath).CorrectTakenDateAsync(movedId, correctedDate, "Album order");

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var paths = await verification.ArchiveFiles
                .OrderBy(file => file.OriginalFileName)
                .Select(file => new { file.OriginalFileName, file.CurrentPath })
                .ToListAsync();

            Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100103 - 2.jpg"),
                paths.Single(file => file.OriginalFileName == "moved.jpg").CurrentPath);
            Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100102 - 1.jpg"),
                paths.Single(file => file.OriginalFileName == "old-day-stays.jpg").CurrentPath);
            Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100103 - 1.jpg"),
                paths.Single(file => file.OriginalFileName == "new-day-first.jpg").CurrentPath);
            Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100103 - 3.jpg"),
                paths.Single(file => file.OriginalFileName == "new-day-last.jpg").CurrentPath);
            Assert.Equal(3, await verification.OperationLogs.CountAsync(log => log.OperationType == "Resequence"));
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
    public async Task CorrectTakenDateAsync_resequence_skips_duplicate_and_unsupported_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-resequence-skip-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var correctedId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                var unsupported = CreateArchivedPhoto(Guid.NewGuid(), root, "notes.txt", "hash-notes", new DateTimeOffset(2010, 1, 4, 7, 0, 0, TimeSpan.Zero), 1);
                unsupported.File.MediaKind = MediaKind.Unsupported;
                unsupported.File.CurrentPath = Path.Combine(root, "Output", "Unsupported", "2010-2019", "2010", "notes.txt");
                var duplicate = CreateArchivedPhoto(Guid.NewGuid(), root, "copy.jpg", "hash-copy", new DateTimeOffset(2010, 1, 4, 8, 0, 0, TimeSpan.Zero), 1);
                duplicate.File.MediaKind = MediaKind.Duplicate;
                duplicate.File.Status = ArchiveFileStatus.Duplicate;
                duplicate.File.CurrentPath = Path.Combine(root, "Output", "Duplicates", "2010-2019", "2010", "copy.jpg");
                var corrected = CreateArchivedPhoto(correctedId, root, "photo.jpg", "hash-photo", new DateTimeOffset(2010, 1, 3, 8, 0, 0, TimeSpan.Zero), 1);

                dbContext.ArchiveFiles.AddRange(
                    corrected.File,
                    unsupported.File,
                    duplicate.File);
                dbContext.PhotoMetadata.AddRange(
                    corrected.Metadata,
                    unsupported.Metadata,
                    duplicate.Metadata);
                await dbContext.SaveChangesAsync();
            }

            await new PhotoReviewRepository(databasePath).CorrectTakenDateAsync(
                correctedId,
                new DateTimeOffset(2010, 1, 4, 9, 0, 0, TimeSpan.Zero),
                "Album order");

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var unsupportedPath = await verification.ArchiveFiles
                .Where(file => file.OriginalFileName == "notes.txt")
                .Select(file => file.CurrentPath)
                .SingleAsync();
            var duplicatePath = await verification.ArchiveFiles
                .Where(file => file.OriginalFileName == "copy.jpg")
                .Select(file => file.CurrentPath)
                .SingleAsync();

            Assert.EndsWith(Path.Combine("Unsupported", "2010-2019", "2010", "notes.txt"), unsupportedPath);
            Assert.EndsWith(Path.Combine("Duplicates", "2010-2019", "2010", "copy.jpg"), duplicatePath);
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
    public async Task CorrectTakenDateAsync_physically_renames_existing_copied_output_files()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-resequence-files-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var movedId = Guid.NewGuid();
            var moved = CreateArchivedPhoto(movedId, root, "moved.jpg", "hash-moved", new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero), 1);
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(moved.File);
                dbContext.PhotoMetadata.Add(moved.Metadata);
                await dbContext.SaveChangesAsync();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(moved.File.CurrentPath!)!);
            await File.WriteAllTextAsync(moved.File.CurrentPath!, "copied image");

            await new PhotoReviewRepository(databasePath).CorrectTakenDateAsync(
                movedId,
                new DateTimeOffset(2010, 1, 3, 8, 0, 0, TimeSpan.Zero),
                "Album order");

            var expectedPath = Path.Combine(root, "Output", "Photos", "2010-2019", "2010", "20100103 - 1.jpg");
            Assert.False(File.Exists(moved.File.CurrentPath!));
            Assert.True(File.Exists(expectedPath));
            Assert.Equal("copied image", await File.ReadAllTextAsync(expectedPath));

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var row = await verification.ArchiveFiles.SingleAsync(file => file.Id == movedId);
            Assert.Equal(expectedPath, row.CurrentPath);
            Assert.Equal(ArchiveFileStatus.Processed, row.Status);
            Assert.Equal(1, await verification.OperationLogs.CountAsync(log => log.OperationType == "Resequence" && log.Result == "Renamed"));
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
    public async Task CorrectTakenDateAsync_logs_collision_and_does_not_overwrite_existing_output_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-resequence-collision-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var movedId = Guid.NewGuid();
            var moved = CreateArchivedPhoto(movedId, root, "moved.jpg", "hash-moved", new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero), 1);
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(moved.File);
                dbContext.PhotoMetadata.Add(moved.Metadata);
                await dbContext.SaveChangesAsync();
            }

            var expectedPath = Path.Combine(root, "Output", "Photos", "2010-2019", "2010", "20100103 - 1.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(moved.File.CurrentPath!)!);
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            await File.WriteAllTextAsync(moved.File.CurrentPath!, "copied image");
            await File.WriteAllTextAsync(expectedPath, "existing image");

            await new PhotoReviewRepository(databasePath).CorrectTakenDateAsync(
                movedId,
                new DateTimeOffset(2010, 1, 3, 8, 0, 0, TimeSpan.Zero),
                "Album order");

            Assert.True(File.Exists(moved.File.CurrentPath!));
            Assert.Equal("existing image", await File.ReadAllTextAsync(expectedPath));

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var row = await verification.ArchiveFiles.SingleAsync(file => file.Id == movedId);
            Assert.Equal(moved.File.CurrentPath, row.CurrentPath);
            Assert.Equal(ArchiveFileStatus.NeedsReview, row.Status);
            Assert.Equal(1, await verification.OperationLogs.CountAsync(log => log.OperationType == "Resequence" && log.Result == "Collision"));
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

            var page = await new PhotoReviewRepository(databasePath).GetPhotoPageAsync(new ReviewFilter(
                IncludeDuplicates: true,
                SortMode: sortMode), 1, 10);

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

    private static (ArchiveFile File, PhotoMetadata Metadata) CreateArchivedPhoto(
        Guid fileId,
        string root,
        string fileName,
        string hash,
        DateTimeOffset takenDate,
        int sequence)
    {
        var originalPath = Path.Combine(root, "Input", fileName);
        var file = new ArchiveFile
        {
            Id = fileId,
            OriginalPath = originalPath,
            CurrentPath = Path.Combine(root, "Output", "Photos", "2010-2019", "2010", $"{takenDate:yyyyMMdd} - {sequence}.jpg"),
            OriginalFileName = fileName,
            Extension = Path.GetExtension(fileName),
            FileSizeBytes = 1,
            Sha256Hash = hash,
            MediaKind = MediaKind.SupportedImage,
            Status = ArchiveFileStatus.Processed
        };

        return (file, new PhotoMetadata
        {
            ArchiveFileId = fileId,
            InferredTakenDate = takenDate,
            DateConfidence = DateConfidence.High
        });
    }
}
