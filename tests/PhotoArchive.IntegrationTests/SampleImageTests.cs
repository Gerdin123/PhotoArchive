using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.App.Review;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;
using PhotoArchive.Infrastructure.Metadata;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.IntegrationTests;

public sealed class SampleImageTests
{
    private static readonly string SampleRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SampleImages"));

    [Fact]
    public async Task Sample_images_classify_supported_files_and_skip_camera_thumbnails()
    {
        var scanner = new FileSystemScanner();
        var classifier = new SimpleFileClassifier();
        var supported = new List<string>();
        var skipped = new List<string>();

        await foreach (var scannedFile in scanner.ScanAsync(SampleRoot))
        {
            if (PreprocessingFileFilter.ShouldSkip(scannedFile))
            {
                skipped.Add(scannedFile.OriginalFileName);
                continue;
            }

            var classification = await classifier.ClassifyAsync(scannedFile);
            if (classification.MediaKind == MediaKind.SupportedImage)
            {
                supported.Add(scannedFile.OriginalFileName);
            }
        }

        Assert.Contains("AUT_0638.JPG", supported);
        Assert.Contains("bilder.tif", supported);
        Assert.Contains("THM_0638.JPG", skipped);
        Assert.DoesNotContain("THM_0638.JPG", supported);
    }

    [Fact]
    public async Task Application_can_write_title_and_tags_to_sample_jpeg()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-sample-write-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(root, "photoarchive.db");
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(SampleRoot, "20010101", "AUT_0638.JPG");
        var imagePath = Path.Combine(root, "AUT_0638.JPG");
        File.Copy(sourcePath, imagePath);

        try
        {
            var fileId = Guid.NewGuid();
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
                    FileSizeBytes = new FileInfo(imagePath).Length,
                    Sha256Hash = "sample-hash",
                    MediaKind = MediaKind.SupportedImage,
                    Status = ArchiveFileStatus.Processed
                });
                dbContext.PhotoMetadata.Add(new PhotoMetadata
                {
                    ArchiveFileId = fileId,
                    InferredTakenDate = new DateTimeOffset(2001, 1, 1, 12, 0, 0, TimeSpan.Zero),
                    DateConfidence = DateConfidence.High
                });
                await dbContext.SaveChangesAsync();
            }

            var repository = new PhotoReviewRepository(databasePath);
            await repository.UpdateTitleAsync(fileId, "New Year Sample");
            await repository.AddTagAsync(fileId, "Family", TagType.Person);
            await repository.AddTagAsync(fileId, "Holiday", TagType.Event);
            await repository.WriteImageMetadataAsync(fileId);

            var bytes = await File.ReadAllBytesAsync(imagePath);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains("http://ns.adobe.com/xap/1.0/", text);
            Assert.Contains("<rdf:li xml:lang=\"x-default\">New Year Sample</rdf:li>", text);
            Assert.Contains("<rdf:li>Family</rdf:li>", text);
            Assert.Contains("<rdf:li>Holiday</rdf:li>", text);
            Assert.False(File.Exists(XmpSidecarMetadataWriter.GetSidecarPath(imagePath)));
            await using var verification = PhotoArchiveDbContextFactory.Create(databasePath);
            Assert.Equal(1, await verification.OperationLogs.CountAsync(log => log.OperationType == "EmbeddedMetadataWrite" && log.Result == "Written"));
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
    public async Task Embedded_metadata_writer_rejects_sample_tiff_until_embedded_tiff_write_is_supported()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-sample-tiff-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var imagePath = Path.Combine(root, "bilder.tif");
        File.Copy(Path.Combine(SampleRoot, "20010101", "bilder.tif"), imagePath);

        try
        {
            var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
                new EmbeddedXmpMetadataWriter().WriteAsync(new MetadataWriteRequest(
                    imagePath,
                    TakenDate: null,
                    PreferSidecar: false,
                    Title: "Unsupported embedded TIFF title",
                    Tags: ["Sample"])));

            Assert.Contains("JPEG", exception.Message);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
