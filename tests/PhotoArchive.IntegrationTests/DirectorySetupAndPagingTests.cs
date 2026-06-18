using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class DirectorySetupAndPagingTests
{
    [Fact]
    public async Task DirectorySetupService_preprocesses_empty_database_and_reuses_existing_database()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });

        try
        {
            var service = new DirectorySetupService();
            var first = await service.OpenOrPreprocessAsync(input, output, databasePath);
            var second = await service.OpenOrPreprocessAsync(input, output, databasePath);

            Assert.True(first.Preprocessed);
            Assert.False(second.Preprocessed);
            Assert.Equal(1, first.FileCount);
            Assert.Equal(1, second.FileCount);

            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            Assert.Equal(1, await dbContext.ArchiveFiles.CountAsync());
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
    public async Task DirectorySetupService_rejects_missing_input_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-missing-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "missing");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                new DirectorySetupService().OpenOrPreprocessAsync(input, output, databasePath));

            Assert.Contains(Path.GetFullPath(input), exception.Message);
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
    public async Task DirectorySetupService_rejects_cleaned_output_inside_original_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-invalid-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(input, "cleaned");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DirectorySetupService().OpenOrPreprocessAsync(input, output, databasePath));

            Assert.Contains("Output path cannot be inside input path", exception.Message);
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
    public async Task Review_repository_returns_paged_sorted_home_results_and_needs_review_filter()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-page-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                for (var i = 0; i < 30; i++)
                {
                    var id = Guid.NewGuid();
                    dbContext.ArchiveFiles.Add(new ArchiveFile
                    {
                        Id = id,
                        OriginalPath = Path.Combine(root, $"photo-{i:00}.jpg"),
                        CurrentPath = Path.Combine(root, $"archive-photo-{i:00}.jpg"),
                        OriginalFileName = $"photo-{i:00}.jpg",
                        Extension = ".jpg",
                        FileSizeBytes = 1,
                        Sha256Hash = $"hash-{i:00}",
                        MediaKind = MediaKind.SupportedImage,
                        Status = i % 10 == 0 ? ArchiveFileStatus.NeedsReview : ArchiveFileStatus.Processed
                    });
                    dbContext.PhotoMetadata.Add(new PhotoMetadata
                    {
                        ArchiveFileId = id,
                        InferredTakenDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i),
                        DateConfidence = i % 10 == 0 ? DateConfidence.Unknown : DateConfidence.High
                    });
                }

                await dbContext.SaveChangesAsync();
            }

            var repository = new PhotoReviewRepository(databasePath);
            var pageTwo = await repository.GetPhotoPageAsync(new ReviewFilter(SortMode: ReviewSortMode.DateDescending), pageNumber: 2, pageSize: 10);
            var needsReview = await repository.GetPhotoPageAsync(new ReviewFilter(UncertainOrUnprocessedOnly: true), pageNumber: 1, pageSize: 20);

            Assert.Equal(30, pageTwo.TotalCount);
            Assert.Equal(2, pageTwo.PageNumber);
            Assert.Equal(10, pageTwo.Photos.Count);
            Assert.Equal("photo-19.jpg", pageTwo.Photos[0].OriginalFileName);
            Assert.Equal(3, needsReview.TotalCount);
            Assert.All(needsReview.Photos, photo => Assert.True(photo.Status == ArchiveFileStatus.NeedsReview || photo.DateConfidence == DateConfidence.Unknown));
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
