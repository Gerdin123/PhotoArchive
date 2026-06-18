namespace PhotoArchive.Cleaner.Models;

internal sealed class ExtractedMetadata
{
    public DateTime? ExifDateTimeOriginal { get; init; }
    public DateTime? ExifCreateDate { get; init; }
    public DateTime? ExifModifyDate { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string CameraMake { get; init; } = string.Empty;
    public string CameraModel { get; init; } = string.Empty;
    public IReadOnlyList<string> ExifTags { get; init; } = [];
}
