namespace PhotoArchive.Infrastructure.Manifest;

public sealed record PreprocessingManifest(
    string AppVersion,
    DateTimeOffset RunTimestampUtc,
    string InputRoot,
    string OutputRoot,
    object Settings,
    IReadOnlyList<ManifestFileRecord> Files);
