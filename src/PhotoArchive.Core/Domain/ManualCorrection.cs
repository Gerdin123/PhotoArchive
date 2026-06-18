namespace PhotoArchive.Core.Domain;

public sealed class ManualCorrection
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ArchiveFileId { get; init; }
    public required string FieldName { get; init; }
    public string? OldValue { get; init; }
    public required string NewValue { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
