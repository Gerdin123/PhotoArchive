using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.IntegrationTests;

public sealed class InfrastructureSafetyTests
{
    [Fact]
    public async Task FileSystemScanner_returns_recursive_files_in_stable_order()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "b"));
        Directory.CreateDirectory(Path.Combine(root, "a"));
        await File.WriteAllTextAsync(Path.Combine(root, "b", "two.txt"), "two");
        await File.WriteAllTextAsync(Path.Combine(root, "a", "one.txt"), "one");

        try
        {
            var files = new List<ScannedFile>();
            await foreach (var file in new FileSystemScanner().ScanAsync(root))
            {
                files.Add(file);
            }

            Assert.Equal(new[] { "one.txt", "two.txt" }, files.Select(file => file.OriginalFileName).ToArray());
            Assert.All(files, file => Assert.True(Path.IsPathFullyQualified(file.FullPath)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("photo.jpg", new byte[] { 0xff, 0xd8, 0xff, 0x00 }, MediaKind.SupportedImage, "signature")]
    [InlineData("photo.png", new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }, MediaKind.SupportedImage, "signature")]
    [InlineData("photo.gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, MediaKind.SupportedImage, "signature")]
    [InlineData("photo.webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 }, MediaKind.SupportedImage, "signature")]
    [InlineData("notes.txt", new byte[] { 0x01, 0x02 }, MediaKind.Unsupported, "Unsupported extension")]
    public async Task SimpleFileClassifier_classifies_supported_signatures_and_unsupported_extensions(
        string fileName,
        byte[] bytes,
        MediaKind expectedKind,
        string expectedReasonFragment)
    {
        var path = Path.Combine(Path.GetTempPath(), $"photoarchive-classify-{Guid.NewGuid():N}-{fileName}");
        await File.WriteAllBytesAsync(path, bytes);

        try
        {
            var scanned = new ScannedFile(path, fileName, Path.GetExtension(fileName), bytes.Length);

            var result = await new SimpleFileClassifier().ClassifyAsync(scanned);

            Assert.Equal(expectedKind, result.MediaKind);
            Assert.Contains(expectedReasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SimpleFileClassifier_keeps_supported_extension_when_signature_is_unknown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"photoarchive-classify-{Guid.NewGuid():N}.jpg");
        await File.WriteAllTextAsync(path, "not a real jpeg");

        try
        {
            var result = await new SimpleFileClassifier().ClassifyAsync(new ScannedFile(path, Path.GetFileName(path), ".jpg", 15));

            Assert.Equal(MediaKind.SupportedImage, result.MediaKind);
            Assert.Contains("signature not recognized", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OutputPlanValidator_rejects_output_inside_input_and_existing_destinations()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-validate-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(input, "cleaned");
        Directory.CreateDirectory(output);
        var source = Path.Combine(input, "photo.jpg");
        var destination = Path.Combine(output, "Photos", "2010-2019", "2010", "20100102 - 1.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(destination, "already exists");

        try
        {
            var plan = new OutputPlan(
                new PreprocessingSettings(input, output, Execute: true),
                DateTimeOffset.UtcNow,
                new[]
                {
                    new PlannedFileOperation(
                        source,
                        destination,
                        MediaKind.SupportedImage,
                        "hash",
                        new DateTimeOffset(2010, 1, 2, 0, 0, 0, TimeSpan.Zero),
                        DateConfidence.High,
                        "EXIF:DateTimeOriginal",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null)
                });

            var errors = new OutputPlanValidator().Validate(plan);

            Assert.Contains(errors, error => error.Contains("Output path cannot be inside input path", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, error => error.Contains("Destination already exists", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OutputPlanValidator_allows_output_inside_input_when_explicitly_configured()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-validate-allow-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(input, "cleaned");
        Directory.CreateDirectory(output);
        var source = Path.Combine(input, "photo.jpg");
        await File.WriteAllTextAsync(source, "source");

        try
        {
            var plan = new OutputPlan(
                new PreprocessingSettings(input, output, Execute: true, AllowOutputInsideInput: true),
                DateTimeOffset.UtcNow,
                new[]
                {
                    new PlannedFileOperation(
                        source,
                        Path.Combine(output, "photo.jpg"),
                        MediaKind.Unsupported,
                        "hash",
                        null,
                        DateConfidence.Unknown,
                        "Unknown",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null)
                });

            var errors = new OutputPlanValidator().Validate(plan);

            Assert.DoesNotContain(errors, error => error.Contains("Output path cannot be inside input path", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveExecutor_does_not_overwrite_existing_destination()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-exec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.jpg");
        var destination = Path.Combine(root, "destination.jpg");
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(destination, "existing");

        try
        {
            var sourceHash = await new FileSystemHashService().ComputeSha256Async(source);
            var plan = new OutputPlan(
                new PreprocessingSettings(root, Path.Combine(root, "out"), Execute: true),
                DateTimeOffset.UtcNow,
                new[]
                {
                    new PlannedFileOperation(
                        source,
                        destination,
                        MediaKind.SupportedImage,
                        sourceHash,
                        null,
                        DateConfidence.Unknown,
                        "Unknown",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null)
                });

            var result = Assert.Single(await new ArchiveExecutor(new FileSystemHashService()).ExecuteAsync(plan));

            Assert.Equal("Failed", result.ExecutionResult);
            Assert.Equal("Destination already exists.", result.ErrorMessage);
            Assert.Equal("existing", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveExecutor_reports_hash_mismatch_after_copy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-exec-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.jpg");
        var destination = Path.Combine(root, "out", "destination.jpg");
        await File.WriteAllTextAsync(source, "source");

        try
        {
            var plan = new OutputPlan(
                new PreprocessingSettings(root, Path.Combine(root, "out"), Execute: true),
                DateTimeOffset.UtcNow,
                new[]
                {
                    new PlannedFileOperation(
                        source,
                        destination,
                        MediaKind.SupportedImage,
                        "not-the-real-hash",
                        null,
                        DateConfidence.Unknown,
                        "Unknown",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null)
                });

            var result = Assert.Single(await new ArchiveExecutor(new FileSystemHashService()).ExecuteAsync(plan));

            Assert.Equal("Failed", result.ExecutionResult);
            Assert.Equal("Copied file hash did not match source hash.", result.ErrorMessage);
            Assert.True(File.Exists(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
