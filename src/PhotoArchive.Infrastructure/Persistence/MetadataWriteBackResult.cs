namespace PhotoArchive.Infrastructure.Persistence;

public sealed record MetadataWriteBackResult(
    int Attempted,
    int Written,
    int Skipped,
    int Failed);
