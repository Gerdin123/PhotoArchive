using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed record ReviewFilter(
    string? SearchText = null,
    ArchiveFileStatus? Status = null,
    Guid? TagId = null,
    IReadOnlyList<Guid>? TagIds = null,
    bool NoTagsOnly = false,
    bool DuplicatesOnly = false,
    bool IncludeDuplicates = false,
    bool IncludeUnsupported = false,
    bool IncludeDeleted = false,
    bool UncertainOrUnprocessedOnly = false,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    ReviewSortMode SortMode = ReviewSortMode.DateAscending);
