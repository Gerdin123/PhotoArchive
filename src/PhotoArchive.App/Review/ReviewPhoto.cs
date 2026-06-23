using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed record ReviewPhoto(
    Guid Id,
    string OriginalPath,
    string? CurrentPath,
    string OriginalFileName,
    MediaKind MediaKind,
    ArchiveFileStatus Status,
    string? Sha256Hash,
    string? ThumbnailPath,
    DateTimeOffset? InferredTakenDate,
    DateConfidence DateConfidence,
    string? Title,
    string Tags)
{
    public string DisplayDate => InferredTakenDate?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? OriginalFileName : Title.Trim();
    public string DisplayTitle => $"{DisplayDate}  {DisplayName}";
}
