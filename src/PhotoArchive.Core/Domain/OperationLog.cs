namespace PhotoArchive.Core.Domain;

public sealed class OperationLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OperationType { get; init; }
    public required string SourcePath { get; init; }
    public string? DestinationPath { get; init; }
    public required string Result { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
