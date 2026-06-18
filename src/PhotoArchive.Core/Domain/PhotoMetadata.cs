namespace PhotoArchive.Core.Domain;

public sealed class PhotoMetadata
{
    public Guid ArchiveFileId { get; init; }
    public DateTimeOffset? ExifDateTimeOriginal { get; set; }
    public DateTimeOffset? ExifCreateDate { get; set; }
    public DateTimeOffset? XmpDateCreated { get; set; }
    public DateTimeOffset? FileCreatedDate { get; set; }
    public DateTimeOffset? FileModifiedDate { get; set; }
    public DateTimeOffset? InferredTakenDate { get; set; }
    public DateConfidence DateConfidence { get; set; } = DateConfidence.Unknown;
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }
}
