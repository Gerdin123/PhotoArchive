using System.Text.Json;
using System.Text.Json.Serialization;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure.Manifest;

public sealed class PreprocessingManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<string> WriteAsync(OutputPlan plan, CancellationToken cancellationToken = default)
    {
        var manifest = new PreprocessingManifest(
            AppVersion: plan.Settings.AppVersion,
            RunTimestampUtc: plan.RunStartedAtUtc,
            InputRoot: plan.Settings.InputRoot,
            OutputRoot: plan.Settings.OutputRoot,
            Settings: new
            {
                plan.Settings.Execute,
                plan.Settings.AllowOutputInsideInput
            },
            Files: plan.Operations.Select(operation => new ManifestFileRecord(
                SourcePath: operation.SourcePath,
                PlannedDestination: operation.DestinationPath,
                Sha256Hash: operation.Sha256Hash,
                MediaKind: operation.MediaKind,
                InferredDate: operation.InferredTakenDate,
                DateConfidence: operation.DateConfidence,
                DateSource: operation.DateSource,
                IsDuplicate: operation.IsDuplicate,
                DuplicateGroupId: operation.DuplicateGroupId,
                CanonicalSourcePath: operation.CanonicalSourcePath,
                ExecutionResult: operation.ExecutionResult,
                Error: operation.ErrorMessage)).ToList());

        var manifestDirectory = Path.Combine(plan.Settings.OutputRoot, "Manifests");
        Directory.CreateDirectory(manifestDirectory);

        var fileName = $"preprocessing-{plan.RunStartedAtUtc:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(manifestDirectory, fileName);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
        return path;
    }
}
