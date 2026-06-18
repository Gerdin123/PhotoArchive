namespace PhotoArchive.Core.Preprocessing;

public sealed record MetadataWriteRequest(
    string FilePath,
    DateTimeOffset? TakenDate,
    bool PreferSidecar);
