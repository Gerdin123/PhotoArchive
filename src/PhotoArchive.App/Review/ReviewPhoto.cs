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
    DateTimeOffset? InferredTakenDate,
    DateConfidence DateConfidence,
    string Tags)
{
    public string DisplayDate => InferredTakenDate?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown";
    public string DisplayTitle => $"{DisplayDate}  {OriginalFileName}";
}
