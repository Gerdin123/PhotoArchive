using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure.Metadata;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class MetadataWriteBackTests
{
    [Fact]
    public async Task XmpSidecarMetadataWriter_writes_standard_date_fields_without_modifying_original()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-xmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var imagePath = Path.Combine(root, "photo.jpg");
        await File.WriteAllTextAsync(imagePath, "original");
        var originalModifiedUtc = File.GetLastWriteTimeUtc(imagePath);

        try
        {
            var date = new DateTimeOffset(2011, 3, 4, 5, 6, 0, TimeSpan.FromHours(1));
            await new XmpSidecarMetadataWriter().WriteAsync(new MetadataWriteRequest(imagePath, date, PreferSidecar: true));

            var sidecarPath = XmpSidecarMetadataWriter.GetSidecarPath(imagePath);
            var sidecar = await File.ReadAllTextAsync(sidecarPath);

            Assert.True(File.Exists(sidecarPath));
            Assert.Contains("<exif:DateTimeOriginal>2011-03-04T05:06:00+01:00</exif:DateTimeOriginal>", sidecar);
            Assert.Contains("<xmp:CreateDate>2011-03-04T05:06:00+01:00</xmp:CreateDate>", sidecar);
            Assert.Equal("original", await File.ReadAllTextAsync(imagePath));
            Assert.Equal(originalModifiedUtc, File.GetLastWriteTimeUtc(imagePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task XmpSidecarMetadataWriter_rejects_missing_taken_date()
    {
        var writer = new XmpSidecarMetadataWriter();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.WriteAsync(new MetadataWriteRequest("photo.jpg", TakenDate: null, PreferSidecar: true)));

        Assert.Contains("Taken date is required", exception.Message);
    }

    [Fact]
    public async Task MetadataWriteBackService_writes_corrected_sidecars_and_logs_results()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-writeback-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);
        var imagePath = Path.Combine(root, "photo.jpg");
        await File.WriteAllTextAsync(imagePath, "original");

        try
        {
            var fileId = Guid.NewGuid();
            var date = new DateTimeOffset(2011, 3, 4, 5, 6, 0, TimeSpan.Zero);
            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.ArchiveFiles.Add(new ArchiveFile
                {
                    Id = fileId,
                    OriginalPath = imagePath,
                    CurrentPath = imagePath,
                    OriginalFileName = Path.GetFileName(imagePath),
                    Extension = ".jpg",
                    FileSizeBytes = 8,
                    Sha256Hash = "hash",
                    MediaKind = MediaKind.SupportedImage,
                    Status = ArchiveFileStatus.Processed
                });
                dbContext.PhotoMetadata.Add(new PhotoMetadata
                {
                    ArchiveFileId = fileId,
                    InferredTakenDate = date,
                    DateConfidence = DateConfidence.High
                });
                dbContext.ManualCorrections.Add(new ManualCorrection
                {
                    ArchiveFileId = fileId,
                    FieldName = nameof(PhotoMetadata.InferredTakenDate),
                    OldValue = null,
                    NewValue = date.ToString("O"),
                    Reason = "Test correction"
                });
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = PhotoArchiveDbContextFactory.Create(databasePath))
            {
                var result = await new MetadataWriteBackService(new XmpSidecarMetadataWriter()).WriteAsync(dbContext);

                Assert.Equal(1, result.Attempted);
                Assert.Equal(1, result.Written);
                Assert.Equal(0, result.Failed);
                Assert.True(File.Exists(XmpSidecarMetadataWriter.GetSidecarPath(imagePath)));
                Assert.Equal(1, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "MetadataWriteBack" && log.Result == "Written"));
            }
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
    public async Task MetadataWriteBackService_skips_missing_target_files_and_logs_result()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-writeback-skip-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);
        var missingImagePath = Path.Combine(root, "missing.jpg");

        try
        {
            var fileId = Guid.NewGuid();
            var date = new DateTimeOffset(2019, 8, 7, 6, 5, 0, TimeSpan.Zero);
            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            await dbContext.Database.MigrateAsync();
            dbContext.ArchiveFiles.Add(new ArchiveFile
            {
                Id = fileId,
                OriginalPath = missingImagePath,
                CurrentPath = missingImagePath,
                OriginalFileName = Path.GetFileName(missingImagePath),
                Extension = ".jpg",
                FileSizeBytes = 8,
                Sha256Hash = "hash",
                MediaKind = MediaKind.SupportedImage,
                Status = ArchiveFileStatus.Processed
            });
            dbContext.PhotoMetadata.Add(new PhotoMetadata
            {
                ArchiveFileId = fileId,
                InferredTakenDate = date,
                DateConfidence = DateConfidence.High
            });
            dbContext.ManualCorrections.Add(new ManualCorrection
            {
                ArchiveFileId = fileId,
                FieldName = nameof(PhotoMetadata.InferredTakenDate),
                NewValue = date.ToString("O"),
                Reason = "Test correction"
            });
            await dbContext.SaveChangesAsync();

            var result = await new MetadataWriteBackService(new XmpSidecarMetadataWriter()).WriteAsync(dbContext);

            Assert.Equal(1, result.Attempted);
            Assert.Equal(0, result.Written);
            Assert.Equal(1, result.Skipped);
            Assert.Equal(0, result.Failed);
            Assert.Equal(1, await dbContext.OperationLogs.CountAsync(log => log.OperationType == "MetadataWriteBack" && log.Result == "Skipped"));
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
    public async Task MetadataWriteBackService_only_writes_corrected_rows_by_default_and_can_write_all_rows()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-writeback-all-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);
        var correctedPath = Path.Combine(root, "corrected.jpg");
        var uncorrectedPath = Path.Combine(root, "uncorrected.jpg");
        await File.WriteAllTextAsync(correctedPath, "corrected");
        await File.WriteAllTextAsync(uncorrectedPath, "uncorrected");

        try
        {
            var correctedId = Guid.NewGuid();
            var uncorrectedId = Guid.NewGuid();
            var date = new DateTimeOffset(2020, 1, 2, 3, 4, 0, TimeSpan.Zero);
            await using var dbContext = PhotoArchiveDbContextFactory.Create(databasePath);
            await dbContext.Database.MigrateAsync();
            AddWriteBackPhoto(dbContext, correctedId, correctedPath, date);
            AddWriteBackPhoto(dbContext, uncorrectedId, uncorrectedPath, date.AddDays(1));
            dbContext.ManualCorrections.Add(new ManualCorrection
            {
                ArchiveFileId = correctedId,
                FieldName = nameof(PhotoMetadata.InferredTakenDate),
                NewValue = date.ToString("O"),
                Reason = "Corrected"
            });
            await dbContext.SaveChangesAsync();

            var service = new MetadataWriteBackService(new XmpSidecarMetadataWriter());
            var correctedOnly = await service.WriteAsync(dbContext);

            Assert.Equal(1, correctedOnly.Attempted);
            Assert.Equal(1, correctedOnly.Written);
            Assert.True(File.Exists(XmpSidecarMetadataWriter.GetSidecarPath(correctedPath)));
            Assert.False(File.Exists(XmpSidecarMetadataWriter.GetSidecarPath(uncorrectedPath)));

            var all = await service.WriteAsync(dbContext, onlyCorrected: false);

            Assert.Equal(2, all.Attempted);
            Assert.Equal(2, all.Written);
            Assert.True(File.Exists(XmpSidecarMetadataWriter.GetSidecarPath(uncorrectedPath)));
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

    private static void AddWriteBackPhoto(
        PhotoArchiveDbContext dbContext,
        Guid fileId,
        string path,
        DateTimeOffset date)
    {
        dbContext.ArchiveFiles.Add(new ArchiveFile
        {
            Id = fileId,
            OriginalPath = path,
            CurrentPath = path,
            OriginalFileName = Path.GetFileName(path),
            Extension = ".jpg",
            FileSizeBytes = 8,
            Sha256Hash = fileId.ToString("N"),
            MediaKind = MediaKind.SupportedImage,
            Status = ArchiveFileStatus.Processed
        });
        dbContext.PhotoMetadata.Add(new PhotoMetadata
        {
            ArchiveFileId = fileId,
            InferredTakenDate = date,
            DateConfidence = DateConfidence.High
        });
    }
}
