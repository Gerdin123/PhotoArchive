using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;
using PhotoArchive.Infrastructure.Manifest;
using System.Text.Json;

namespace PhotoArchive.IntegrationTests;

public sealed class PreprocessingMilestoneTests
{
    [Fact]
    public async Task Manifest_and_executor_complete_copy_plan()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-m1-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(input);

        try
        {
            var source = Path.Combine(input, "IMG_20100102.jpg");
            await File.WriteAllBytesAsync(source, new byte[] { 0xff, 0xd8, 0xff, 0x01 });

            var hashService = new FileSystemHashService();
            var hash = await hashService.ComputeSha256Async(source);
            var date = new DateTimeOffset(2010, 1, 2, 8, 0, 0, TimeSpan.Zero);
            var analyzed = new AnalyzedFile(
                new ScannedFile(source, Path.GetFileName(source), ".jpg", 4),
                MediaKind.SupportedImage,
                hash,
                new DateInferenceEvidence(Path.GetFileName(source), ExifDateTimeOriginal: date),
                new DateInferenceResult(date, DateConfidence.High, "EXIF:DateTimeOriginal"));

            var plan = new OutputPlanner().CreatePlan(new PreprocessingRun(
                new PreprocessingSettings(input, output, Execute: true),
                new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
                new[] { analyzed }));

            var manifestPath = await new PreprocessingManifestWriter().WriteAsync(plan);
            var executed = await new ArchiveExecutor(hashService).ExecuteAsync(plan);
            var executedPlan = plan with { Operations = executed };
            var logPath = await new OperationLogWriter().WriteAsync(executedPlan);

            Assert.True(File.Exists(manifestPath));
            Assert.True(File.Exists(logPath));
            Assert.Equal("Copied", Assert.Single(executed).ExecutionResult);
            Assert.True(File.Exists(Assert.Single(executed).DestinationPath));
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
    public async Task Dry_run_manifest_contains_settings_file_evidence_and_planned_destination()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-manifest-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(input);

        try
        {
            var source = Path.Combine(input, "IMG_19991231.jpg");
            await File.WriteAllBytesAsync(source, new byte[] { 0xff, 0xd8, 0xff, 0x01 });
            var hash = await new FileSystemHashService().ComputeSha256Async(source);
            var date = new DateTimeOffset(1999, 12, 31, 23, 59, 0, TimeSpan.Zero);
            var analyzed = new AnalyzedFile(
                new ScannedFile(source, Path.GetFileName(source), ".jpg", 4),
                MediaKind.SupportedImage,
                hash,
                new DateInferenceEvidence(Path.GetFileName(source), ExifDateTimeOriginal: date),
                new DateInferenceResult(date, DateConfidence.High, "EXIF:DateTimeOriginal"));

            var plan = new OutputPlanner().CreatePlan(new PreprocessingRun(
                new PreprocessingSettings(input, output, Execute: false),
                new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
                new[] { analyzed }));

            var manifestPath = await new PreprocessingManifestWriter().WriteAsync(plan);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var rootElement = document.RootElement;
            var file = rootElement.GetProperty("Files")[0];

            Assert.Equal("0.1.0", rootElement.GetProperty("AppVersion").GetString());
            Assert.Equal(input, rootElement.GetProperty("InputRoot").GetString());
            Assert.False(rootElement.GetProperty("Settings").GetProperty("Execute").GetBoolean());
            Assert.Equal(source, file.GetProperty("SourcePath").GetString());
            Assert.Equal(hash, file.GetProperty("Sha256Hash").GetString());
            Assert.Equal("SupportedImage", file.GetProperty("MediaKind").GetString());
            Assert.Equal("High", file.GetProperty("DateConfidence").GetString());
            Assert.EndsWith(Path.Combine("Photos", "1990-1999", "1999", "19991231 - 1.jpg"), file.GetProperty("PlannedDestination").GetString());
            Assert.Equal("Planned", file.GetProperty("ExecutionResult").GetString());
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
