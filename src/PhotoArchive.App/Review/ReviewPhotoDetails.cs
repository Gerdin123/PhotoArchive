using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed record ReviewPhotoDetails(
    ReviewPhoto Photo,
    PhotoMetadata? Metadata,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<ReviewPhoto> NearbyPhotos,
    IReadOnlyList<RelatedReviewPhoto> RelatedPhotos,
    IReadOnlyList<ReviewPhoto> DuplicateGroup);
