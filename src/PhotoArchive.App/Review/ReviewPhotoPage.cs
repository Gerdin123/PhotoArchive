namespace PhotoArchive.App.Review;

public sealed record ReviewPhotoPage(
    IReadOnlyList<ReviewPhoto> Photos,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
