namespace PhotoArchive.Core.Domain;

public sealed class PhotoTag
{
    public Guid ArchiveFileId { get; init; }
    public Guid TagId { get; init; }
}
