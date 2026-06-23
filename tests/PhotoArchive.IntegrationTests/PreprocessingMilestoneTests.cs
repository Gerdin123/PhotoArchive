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

    [Fact]
    public async Task Preprocessing_manifest_matches_representative_golden_json()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-manifest-golden-{Guid.NewGuid():N}");
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");

        try
        {
            var takenDate = new DateTimeOffset(2007, 4, 14, 9, 30, 0, TimeSpan.Zero);
            var plan = new OutputPlan(
                new PreprocessingSettings(input, output, Execute: false),
                new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
                [
                    new PlannedFileOperation(
                        SourcePath: Path.Combine(input, "photo.jpg"),
                        DestinationPath: Path.Combine(output, "Photos", "2000-2009", "2007", "20070414 - 1.jpg"),
                        MediaKind: MediaKind.SupportedImage,
                        Sha256Hash: "hash-canonical",
                        InferredTakenDate: takenDate,
                        DateConfidence: DateConfidence.High,
                        DateSource: "EXIF:DateTimeOriginal",
                        IsDuplicate: false,
                        CanonicalSourcePath: null,
                        DuplicateGroupId: null),
                    new PlannedFileOperation(
                        SourcePath: Path.Combine(input, "copy.jpg"),
                        DestinationPath: Path.Combine(output, "Duplicates", "2000-2009", "2007", "copy.jpg"),
                        MediaKind: MediaKind.Duplicate,
                        Sha256Hash: "hash-canonical",
                        InferredTakenDate: takenDate,
                        DateConfidence: DateConfidence.High,
                        DateSource: "EXIF:DateTimeOriginal",
                        IsDuplicate: true,
                        CanonicalSourcePath: Path.Combine(input, "photo.jpg"),
                        DuplicateGroupId: "hash-canonical")
                ]);

            var manifestPath = await new PreprocessingManifestWriter().WriteAsync(plan);
            var actual = NormalizeGoldenJson(await File.ReadAllTextAsync(manifestPath), root);

            const string expected = """
{
  "AppVersion": "0.1.0",
  "RunTimestampUtc": "2026-06-18T12:00:00+00:00",
  "InputRoot": "<root>/input",
  "OutputRoot": "<root>/output",
  "Settings": {
    "Execute": false,
    "AllowOutputInsideInput": false
  },
  "Files": [
    {
      "SourcePath": "<root>/input/photo.jpg",
      "PlannedDestination": "<root>/output/Photos/2000-2009/2007/20070414 - 1.jpg",
      "Sha256Hash": "hash-canonical",
      "MediaKind": "SupportedImage",
      "InferredDate": "2007-04-14T09:30:00+00:00",
      "DateConfidence": "High",
      "DateSource": "EXIF:DateTimeOriginal",
      "IsDuplicate": false,
      "DuplicateGroupId": null,
      "CanonicalSourcePath": null,
      "ExecutionResult": "Planned",
      "Error": null
    },
    {
      "SourcePath": "<root>/input/copy.jpg",
      "PlannedDestination": "<root>/output/Duplicates/2000-2009/2007/copy.jpg",
      "Sha256Hash": "hash-canonical",
      "MediaKind": "Duplicate",
      "InferredDate": "2007-04-14T09:30:00+00:00",
      "DateConfidence": "High",
      "DateSource": "EXIF:DateTimeOriginal",
      "IsDuplicate": true,
      "DuplicateGroupId": "hash-canonical",
      "CanonicalSourcePath": "<root>/input/photo.jpg",
      "ExecutionResult": "Planned",
      "Error": null
    }
  ]
}
""";

            Assert.Equal(NormalizeLineEndings(expected), actual);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string NormalizeGoldenJson(string json, string root)
    {
        return NormalizeLineEndings(json)
            .Replace(root.Replace("\\", "\\\\"), "<root>", StringComparison.OrdinalIgnoreCase)
            .Replace("\\\\", "/", StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
