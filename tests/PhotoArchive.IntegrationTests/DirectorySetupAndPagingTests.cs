using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Diagnostics;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Infrastructure.Persistence;
using SkiaSharp;

namespace PhotoArchive.IntegrationTests;

public sealed class DirectorySetupAndPagingTests
{
    [Fact]
    public void DirectorySetupDefaults_places_cleaned_folder_next_to_input_and_database_inside_cleaned_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-defaults-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "folder");

        var defaults = DirectorySetupDefaults.FromInputRoot(input);

        Assert.Equal(Path.GetFullPath(input), defaults.InputRoot);
        Assert.Equal(Path.GetFullPath(input) + "cleaned", defaults.OutputRoot);
        Assert.Equal(Path.Combine(Path.GetFullPath(input) + "cleaned", "photoarchive.db"), defaults.DatabasePath);
    }

    [Fact]
    public void DirectorySetupSettingsStore_persists_last_selected_paths_and_ignores_corrupt_json()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(root, "settings.json");

        try
        {
            var store = new DirectorySetupSettingsStore(settingsPath);
            var settings = new DirectorySetupSettings(
                InputRoot: Path.Combine(root, "input"),
                OutputRoot: Path.Combine(root, "output"),
                DatabasePath: Path.Combine(root, "output", "photoarchive.db"));

            store.Save(settings);

            Assert.Equal(settings, store.Load());

            File.WriteAllText(settingsPath, "{not json");
            var fallback = store.Load();
            Assert.Null(fallback.InputRoot);
            Assert.Null(fallback.OutputRoot);
            Assert.Null(fallback.DatabasePath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

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
            var file = await dbContext.ArchiveFiles.SingleAsync();
            Assert.Equal(ThumbnailStatus.Failed, file.ThumbnailStatus);
            Assert.Null(file.ThumbnailPath);
            Assert.Equal(2, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "Thumbnail" && log.Result == "Failed"));
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
    public async Task DirectorySetupService_persists_generated_thumbnails_and_visual_metadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-thumbnail-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        WriteTestImage(Path.Combine(input, "IMG_20100102.jpg"));

        try
        {
            var result = await new DirectorySetupService().OpenOrPreprocessAsync(input, output, databasePath);

            Assert.True(result.Preprocessed);
            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            var file = await dbContext.ArchiveFiles.SingleAsync();
            var metadata = await dbContext.PhotoMetadata.SingleAsync();
            Assert.Equal(ThumbnailStatus.Generated, file.ThumbnailStatus);
            Assert.NotNull(file.ThumbnailPath);
            Assert.True(File.Exists(file.ThumbnailPath));
            Assert.Matches("^#[0-9A-F]{6}$", metadata.AverageColorHex);
            Assert.Matches("^[0-9A-F]{16}$", metadata.PerceptualHash);
            Assert.Equal(1, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "Thumbnail" && log.Result == "Generated"));
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
    public async Task DirectorySetupService_regenerates_missing_thumbnails_when_opening_existing_database()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-thumbnail-open-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        var currentPath = Path.Combine(output, "Photos", "2010-2019", "2010", "20100102 - 1.jpg");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
        WriteTestImage(currentPath);

        try
        {
            var fileId = Guid.NewGuid();
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(new ArchiveFile
                {
                    Id = fileId,
                    OriginalPath = Path.Combine(input, "IMG_20100102.jpg"),
                    CurrentPath = currentPath,
                    OriginalFileName = "IMG_20100102.jpg",
                    Extension = ".jpg",
                    FileSizeBytes = new FileInfo(currentPath).Length,
                    Sha256Hash = "hash",
                    MediaKind = MediaKind.SupportedImage,
                    Status = ArchiveFileStatus.Processed,
                    ThumbnailStatus = ThumbnailStatus.Failed
                });
                dbContext.PhotoMetadata.Add(new PhotoMetadata
                {
                    ArchiveFileId = fileId,
                    InferredTakenDate = new DateTimeOffset(2010, 1, 2, 0, 0, 0, TimeSpan.Zero),
                    DateConfidence = DateConfidence.High
                });
                await dbContext.SaveChangesAsync();
            }

            var result = await new DirectorySetupService().OpenOrPreprocessAsync(input, output, databasePath);

            Assert.False(result.Preprocessed);
            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var file = await verification.ArchiveFiles.SingleAsync();
            var metadata = await verification.PhotoMetadata.SingleAsync();
            Assert.Equal(ThumbnailStatus.Generated, file.ThumbnailStatus);
            Assert.True(File.Exists(file.ThumbnailPath));
            Assert.Matches("^#[0-9A-F]{6}$", metadata.AverageColorHex);
            Assert.Matches("^[0-9A-F]{16}$", metadata.PerceptualHash);
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
    public async Task DirectorySetupService_force_clean_reprocesses_existing_database_and_removes_managed_output()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-force-clean-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(Path.Combine(output, "Photos"));
        Directory.CreateDirectory(Path.Combine(output, "Thumbnails"));
        Directory.CreateDirectory(Path.Combine(output, "KeepMe"));
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        await File.WriteAllBytesAsync(Path.Combine(input, "THM_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        await File.WriteAllTextAsync(Path.Combine(output, "Photos", "stale.txt"), "old run");
        await File.WriteAllTextAsync(Path.Combine(output, "Thumbnails", "stale.jpg"), "old thumbnail");
        await File.WriteAllTextAsync(Path.Combine(output, "KeepMe", "notes.txt"), "not managed by PhotoArchive");

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(new ArchiveFile
                {
                    OriginalPath = Path.Combine(input, "old.jpg"),
                    CurrentPath = Path.Combine(output, "Photos", "old.jpg"),
                    OriginalFileName = "old.jpg",
                    Extension = ".jpg",
                    FileSizeBytes = 1,
                    Sha256Hash = "old",
                    MediaKind = MediaKind.SupportedImage,
                    Status = ArchiveFileStatus.Processed
                });
                await dbContext.SaveChangesAsync();
            }

            SqliteConnection.ClearAllPools();
            var result = await new DirectorySetupService().OpenOrPreprocessAsync(
                input,
                output,
                databasePath,
                forceClean: true);

            Assert.True(result.Preprocessed);
            Assert.Equal(1, result.FileCount);
            Assert.False(File.Exists(Path.Combine(output, "Photos", "stale.txt")));
            Assert.False(File.Exists(Path.Combine(output, "Thumbnails", "stale.jpg")));
            Assert.True(File.Exists(Path.Combine(output, "KeepMe", "notes.txt")));

            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            var names = await verification.ArchiveFiles.Select(file => file.OriginalFileName).ToListAsync();
            Assert.Equal(["IMG_20100102.jpg"], names);
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
    public async Task DirectorySetupService_force_clean_can_remove_database_after_existing_archive_was_opened()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-force-clean-open-db-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(new ArchiveFile
                {
                    OriginalPath = Path.Combine(input, "old.jpg"),
                    CurrentPath = Path.Combine(output, "Photos", "old.jpg"),
                    OriginalFileName = "old.jpg",
                    Extension = ".jpg",
                    FileSizeBytes = 1,
                    Sha256Hash = "old",
                    MediaKind = MediaKind.SupportedImage,
                    Status = ArchiveFileStatus.Processed
                });
                await dbContext.SaveChangesAsync();
            }

            var service = new DirectorySetupService();
            var opened = await service.OpenOrPreprocessAsync(input, output, databasePath);
            var cleaned = await service.OpenOrPreprocessAsync(input, output, databasePath, forceClean: true);

            Assert.False(opened.Preprocessed);
            Assert.True(cleaned.Preprocessed);
            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            Assert.Equal(["IMG_20100102.jpg"], await verification.ArchiveFiles
                .Select(file => file.OriginalFileName)
                .ToListAsync());
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
    public async Task DirectorySetupService_force_clean_rejects_output_or_database_inside_input()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-force-clean-invalid-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        Directory.CreateDirectory(input);

        try
        {
            var service = new DirectorySetupService();
            var outputInsideInput = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.OpenOrPreprocessAsync(
                    input,
                    Path.Combine(input, "cleaned"),
                    Path.Combine(root, "photoarchive.db"),
                    forceClean: true));
            var databaseInsideInput = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.OpenOrPreprocessAsync(
                    input,
                    Path.Combine(root, "output"),
                    Path.Combine(input, "photoarchive.db"),
                    forceClean: true));
            var sameFolder = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.OpenOrPreprocessAsync(
                    input,
                    input,
                    Path.Combine(root, "photoarchive.db"),
                    forceClean: true));

            Assert.Contains("non-overlapping", outputInsideInput.Message);
            Assert.Contains("SQLite database", databaseInsideInput.Message);
            Assert.Contains("non-overlapping", sameFolder.Message);
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
    public async Task DirectorySetupService_force_clean_rejects_output_parent_of_input_without_deleting_input()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-force-clean-parent-{Guid.NewGuid():N}");
        var output = root;
        var input = Path.Combine(root, "Photos");
        var originalFile = Path.Combine(input, "original.jpg");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllTextAsync(originalFile, "original");

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DirectorySetupService().OpenOrPreprocessAsync(
                    input,
                    output,
                    databasePath,
                    forceClean: true));

            Assert.Contains("non-overlapping", exception.Message);
            Assert.True(File.Exists(originalFile));
            Assert.False(File.Exists(databasePath));
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
    public async Task DirectorySetupService_creates_database_parent_folder_for_default_cleaned_database()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-db-folder-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "folder");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        var defaults = DirectorySetupDefaults.FromInputRoot(input);

        try
        {
            var result = await new DirectorySetupService().OpenOrPreprocessAsync(
                defaults.InputRoot,
                defaults.OutputRoot,
                defaults.DatabasePath);

            Assert.True(result.Preprocessed);
            Assert.True(File.Exists(defaults.DatabasePath));
            Assert.StartsWith(defaults.OutputRoot, defaults.DatabasePath, StringComparison.OrdinalIgnoreCase);
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
    public async Task DirectorySetupService_reports_progress_for_preprocessing_phases()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-progress-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        await File.WriteAllTextAsync(Path.Combine(input, "notes.txt"), "unsupported");
        var reports = new List<DirectorySetupProgress>();

        try
        {
            var result = await new DirectorySetupService(new FileApplicationLogger(Path.Combine(root, "logs")))
                .OpenOrPreprocessAsync(input, output, databasePath, new CapturingProgress(reports));

            Assert.True(result.Preprocessed);
            Assert.Equal(1, result.ImagesLeft);
            Assert.Equal(0, result.Duplicates);
            Assert.Equal(1, result.UnsupportedFiles);
            Assert.True(result.Elapsed >= TimeSpan.Zero);
            Assert.Contains(reports, report => report.Phase == "Preparing" && report.Percentage == 100d);
            Assert.Contains(reports, report => report.Phase == "Scanning" && report.FilesFound == 2 && report.Percentage == 100d);
            Assert.Contains(reports, report => report.Phase == "Analyzing" && report.FilesProcessed == 2 && report.Percentage == 100d);
            Assert.Contains(reports, report => report.Phase == "Writing manifest" && report.Percentage == 100d);
            Assert.Contains(reports, report => report.Phase == "Copying" && report.TotalFiles == 2);
            Assert.Contains(reports, report => report.Phase == "Writing final manifest" && report.Percentage == 100d);
            Assert.Contains(reports, report => report.Phase == "Importing" && report.Percentage == 100d);
            var complete = Assert.Single(reports, report => report.Phase == "Complete");
            Assert.Equal(2, complete.FilesFound);
            Assert.Equal(1, complete.FilesProcessed);
            Assert.Equal(100d, complete.Percentage);
            Assert.True(complete.Elapsed >= TimeSpan.Zero);
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
    public async Task DirectorySetupService_skips_thumbnail_and_system_artifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-setup-skip-thumbs-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(input);
        await File.WriteAllBytesAsync(Path.Combine(input, "IMG_20100102.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        await File.WriteAllTextAsync(Path.Combine(input, "Thumbs.db"), "windows thumbnail cache");
        await File.WriteAllTextAsync(Path.Combine(input, "movie.THM"), "camera thumbnail sidecar");
        await File.WriteAllBytesAsync(Path.Combine(input, "thumb.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        await File.WriteAllBytesAsync(Path.Combine(input, "THM_2812.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0x01 });
        var reports = new List<DirectorySetupProgress>();

        try
        {
            var result = await new DirectorySetupService(new FileApplicationLogger(Path.Combine(root, "logs")))
                .OpenOrPreprocessAsync(input, output, databasePath, new CapturingProgress(reports));

            Assert.Equal(1, result.FileCount);
            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            var files = await dbContext.ArchiveFiles
                .OrderBy(file => file.OriginalFileName)
                .Select(file => file.OriginalFileName)
                .ToListAsync();
            Assert.Equal(["IMG_20100102.jpg"], files);
            var scanning = Assert.Single(reports, report => report.Phase == "Scanning");
            Assert.Equal(5, scanning.FilesFound);
            Assert.Equal(5, scanning.TotalFiles);
            Assert.Equal(100d, scanning.Percentage);
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
    public void FileApplicationLogger_writes_structured_daily_log_file()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-logs-{Guid.NewGuid():N}");

        try
        {
            var logger = new FileApplicationLogger(root);
            logger.Info("TestSource", "Started");
            logger.Warning("TestSource", "Careful");
            logger.Error("TestSource", "Failed", new InvalidOperationException("Boom"));

            var logPath = Assert.Single(Directory.EnumerateFiles(root, "photoarchive-*.log"));
            var text = File.ReadAllText(logPath);
            Assert.Contains(" INF TestSource Started", text);
            Assert.Contains(" WRN TestSource Careful", text);
            Assert.Contains(" ERR TestSource Failed", text);
            Assert.Contains("InvalidOperationException", text);
        }
        finally
        {
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

    [Fact]
    public async Task Review_repository_counts_and_pages_beyond_five_thousand_rows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-page-large-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);

        try
        {
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                for (var i = 0; i < 5015; i++)
                {
                    var id = Guid.NewGuid();
                    dbContext.ArchiveFiles.Add(new ArchiveFile
                    {
                        Id = id,
                        OriginalPath = Path.Combine(root, $"photo-{i:0000}.jpg"),
                        CurrentPath = Path.Combine(root, $"archive-photo-{i:0000}.jpg"),
                        OriginalFileName = $"photo-{i:0000}.jpg",
                        Extension = ".jpg",
                        FileSizeBytes = 1,
                        Sha256Hash = $"hash-{i:0000}",
                        MediaKind = MediaKind.SupportedImage,
                        Status = ArchiveFileStatus.Processed
                    });
                    dbContext.PhotoMetadata.Add(new PhotoMetadata
                    {
                        ArchiveFileId = id,
                        InferredTakenDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(i),
                        DateConfidence = DateConfidence.High
                    });
                }

                await dbContext.SaveChangesAsync();
            }

            var page = await new PhotoReviewRepository(databasePath).GetPhotoPageAsync(
                new ReviewFilter(SortMode: ReviewSortMode.DateAscending),
                pageNumber: 251,
                pageSize: 20);

            Assert.Equal(5015, page.TotalCount);
            Assert.Equal(251, page.PageNumber);
            Assert.Equal(251, page.TotalPages);
            Assert.Equal(15, page.Photos.Count);
            Assert.Equal("photo-5000.jpg", page.Photos[0].OriginalFileName);
            Assert.Equal(5015, page.Summary.ArchiveFiles);
            Assert.Equal(5015, page.Summary.SupportedImages);
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

    private sealed class CapturingProgress : IProgress<DirectorySetupProgress>
    {
        private readonly List<DirectorySetupProgress> reports;

        public CapturingProgress(List<DirectorySetupProgress> reports)
        {
            this.reports = reports;
        }

        public void Report(DirectorySetupProgress value)
        {
            reports.Add(value);
        }
    }

    private static void WriteTestImage(string path)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality: 90);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
