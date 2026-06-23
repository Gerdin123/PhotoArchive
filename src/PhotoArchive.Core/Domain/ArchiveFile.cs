namespace PhotoArchive.Core.Domain;

public sealed class ArchiveFile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OriginalPath { get; init; }
    public string? CurrentPath { get; set; }
    public required string OriginalFileName { get; init; }
    public required string Extension { get; init; }
    public long FileSizeBytes { get; init; }
    public string? Sha256Hash { get; set; }
    public MediaKind MediaKind { get; set; } = MediaKind.Unknown;
    public ArchiveFileStatus Status { get; set; } = ArchiveFileStatus.Scanned;
    public string? ThumbnailPath { get; set; }
    public ThumbnailStatus ThumbnailStatus { get; set; } = ThumbnailStatus.NotCreated;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
