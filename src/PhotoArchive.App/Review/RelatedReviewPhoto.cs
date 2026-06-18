namespace PhotoArchive.App.Review;

public sealed record RelatedReviewPhoto(
    ReviewPhoto Photo,
    string Reasons,
    int Score)
{
    public string DisplayTitle => $"{Photo.DisplayTitle}  [{Reasons}]";
}
