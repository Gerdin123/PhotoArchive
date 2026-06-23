namespace PhotoArchive.App.Review;

public sealed record ReviewPhotoPage(
    IReadOnlyList<ReviewPhoto> Photos,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public ReviewPhotoPageSummary Summary { get; init; } = ReviewPhotoPageSummary.Empty;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record ReviewPhotoPageSummary(
    int ArchiveFiles,
    int SupportedImages,
    int DuplicateFiles,
    int UnsupportedFiles,
    int DeletedFiles)
{
    public static ReviewPhotoPageSummary Empty { get; } = new(0, 0, 0, 0, 0);
}
