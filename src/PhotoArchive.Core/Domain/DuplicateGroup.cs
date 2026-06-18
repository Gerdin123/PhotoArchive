namespace PhotoArchive.Core.Domain;

public sealed class DuplicateGroup
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Hash { get; init; }
    public Guid? CanonicalFileId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
