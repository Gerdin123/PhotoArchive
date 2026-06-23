namespace PhotoArchive.Core.Preprocessing;

public sealed record MetadataWriteRequest(
    string FilePath,
    DateTimeOffset? TakenDate,
    bool PreferSidecar,
    string? Title = null,
    IReadOnlyList<string>? Tags = null);
