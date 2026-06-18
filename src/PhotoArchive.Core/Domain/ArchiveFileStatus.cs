namespace PhotoArchive.Core.Domain;

public enum ArchiveFileStatus
{
    Scanned,
    Planned,
    Processed,
    NeedsReview,
    Deleted,
    Duplicate
}
