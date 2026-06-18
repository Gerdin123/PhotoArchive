using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed record ReviewFilter(
    string? SearchText = null,
    ArchiveFileStatus? Status = null,
    Guid? TagId = null,
    bool DuplicatesOnly = false,
    bool UncertainOrUnprocessedOnly = false,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    ReviewSortMode SortMode = ReviewSortMode.DateAscending);
