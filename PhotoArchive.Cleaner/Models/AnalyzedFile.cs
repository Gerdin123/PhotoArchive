namespace PhotoArchive.Cleaner.Models;

internal sealed class AnalyzedFile
{
    public string SourcePath { get; init; } = string.Empty;
    public FileType FileType { get; init; }
    public DateTime GroupingDate { get; init; }
    public string GroupingDateSource { get; init; } = string.Empty;
    public DateTime? ExifDateTimeOriginal { get; init; }
    public DateTime? ExifCreateDate { get; init; }
    public DateTime? ExifModifyDate { get; init; }
    public DateTime? FolderDateCandidate { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastWriteAtUtc { get; init; }
    public long SizeBytes { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string CameraMake { get; init; } = string.Empty;
    public string CameraModel { get; init; } = string.Empty;
    public IReadOnlyList<string> ExifTags { get; init; } = [];
    public string PerceptualHash { get; init; } = string.Empty;
}
