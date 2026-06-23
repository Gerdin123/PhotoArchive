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
            await repository.UpdateTitleAsync(canonicalId, "Album Cover");
            var tag = await repository.AddTagAsync(canonicalId, "Family", TagType.Event);
            var untagged = await repository.GetPhotosAsync(new ReviewFilter(NoTagsOnly: true));
            await repository.MarkDuplicateAsync(duplicateId, canonicalId);

            var tagged = await repository.GetPhotosAsync(new ReviewFilter(TagId: tag.Id));
            var duplicates = await repository.GetPhotosAsync(new ReviewFilter(DuplicatesOnly: true));
            var titled = await repository.GetPhotosAsync(new ReviewFilter(SearchText: "Album Cover"));

            Assert.Single(untagged);
            Assert.Equal(duplicateId, untagged[0].Id);
            Assert.Single(tagged);
            Assert.Equal(canonicalId, tagged[0].Id);
            Assert.Equal("Album Cover", tagged[0].DisplayName);
            Assert.Equal(canonicalId, Assert.Single(titled).Id);
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

    [Fact]
    public async Task Review_repository_excludes_duplicates_by_default_and_can_include_or_filter_them()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-review-duplicates-default-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var canonicalId = Guid.NewGuid();
            var duplicateId = Guid.NewGuid();
            await SeedDatabaseAsync(databasePath, canonicalId, duplicateId);

            var repository = new PhotoReviewRepository(databasePath);
            await repository.InitializeAsync();
            await repository.MarkDuplicateAsync(duplicateId, canonicalId);

            var defaultPhotos = await repository.GetPhotosAsync(new ReviewFilter());
            var includedPhotos = await repository.GetPhotosAsync(new ReviewFilter(IncludeDuplicates: true));
            var duplicatePhotos = await repository.GetPhotosAsync(new ReviewFilter(DuplicatesOnly: true));

            Assert.Single(defaultPhotos);
            Assert.Equal(canonicalId, defaultPhotos[0].Id);
            Assert.Equal(2, includedPhotos.Count);
            Assert.Single(duplicatePhotos);
            Assert.Equal(duplicateId, duplicatePhotos[0].Id);
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
    public async Task Review_repository_defaults_home_to_supported_visible_images_and_allows_opt_in_hidden_kinds()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-review-home-visibility-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var supportedId = Guid.NewGuid();
            var unsupportedId = Guid.NewGuid();
            var unknownId = Guid.NewGuid();
            var deletedId = Guid.NewGuid();
            var duplicateId = Guid.NewGuid();

            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.AddRange(
                    CreateArchiveFile(supportedId, root, "supported.jpg", MediaKind.SupportedImage, ArchiveFileStatus.Processed),
                    CreateArchiveFile(unsupportedId, root, "notes.txt", MediaKind.Unsupported, ArchiveFileStatus.NeedsReview),
                    CreateArchiveFile(unknownId, root, "mystery.bin", MediaKind.Unknown, ArchiveFileStatus.NeedsReview),
                    CreateArchiveFile(deletedId, root, "hidden.jpg", MediaKind.SupportedImage, ArchiveFileStatus.Deleted),
                    CreateArchiveFile(duplicateId, root, "copy.jpg", MediaKind.Duplicate, ArchiveFileStatus.Duplicate));
                dbContext.PhotoMetadata.AddRange(
                    CreateMetadata(supportedId, 0),
                    CreateMetadata(unsupportedId, 1),
                    CreateMetadata(unknownId, 2),
                    CreateMetadata(deletedId, 3),
                    CreateMetadata(duplicateId, 4));
                await dbContext.SaveChangesAsync();
            }

            var repository = new PhotoReviewRepository(databasePath);
            var defaultPage = await repository.GetPhotoPageAsync(new ReviewFilter(), pageNumber: 1, pageSize: 10);
            var withUnsupported = await repository.GetPhotoPageAsync(new ReviewFilter(IncludeUnsupported: true), pageNumber: 1, pageSize: 10);
            var withDeleted = await repository.GetPhotoPageAsync(new ReviewFilter(IncludeDeleted: true), pageNumber: 1, pageSize: 10);
            var withDuplicates = await repository.GetPhotoPageAsync(new ReviewFilter(IncludeDuplicates: true), pageNumber: 1, pageSize: 10);
            var duplicatesOnly = await repository.GetPhotoPageAsync(new ReviewFilter(DuplicatesOnly: true), pageNumber: 1, pageSize: 10);

            Assert.Equal([supportedId], defaultPage.Photos.Select(photo => photo.Id).ToArray());
            Assert.Equal(3, withUnsupported.TotalCount);
            Assert.Contains(withUnsupported.Photos, photo => photo.Id == unsupportedId);
            Assert.Contains(withUnsupported.Photos, photo => photo.Id == unknownId);
            Assert.Equal(2, withDeleted.TotalCount);
            Assert.Contains(withDeleted.Photos, photo => photo.Id == deletedId);
            Assert.Equal(2, withDuplicates.TotalCount);
            Assert.Contains(withDuplicates.Photos, photo => photo.Id == duplicateId);
            Assert.Equal([duplicateId], duplicatesOnly.Photos.Select(photo => photo.Id).ToArray());
            Assert.Equal(5, defaultPage.Summary.ArchiveFiles);
            Assert.Equal(1, defaultPage.Summary.SupportedImages);
            Assert.Equal(1, defaultPage.Summary.DuplicateFiles);
            Assert.Equal(2, defaultPage.Summary.UnsupportedFiles);
            Assert.Equal(1, defaultPage.Summary.DeletedFiles);
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

    private static ArchiveFile CreateArchiveFile(
        Guid fileId,
        string root,
        string fileName,
        MediaKind mediaKind,
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
            MediaKind = mediaKind,
            Status = status
        };
    }

    private static PhotoMetadata CreateMetadata(Guid fileId, int dayOffset)
    {
        return new PhotoMetadata
        {
            ArchiveFileId = fileId,
            InferredTakenDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayOffset),
            DateConfidence = DateConfidence.High
        };
    }
}
