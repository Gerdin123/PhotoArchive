namespace PhotoArchive.Core.Preprocessing;

public sealed record OutputPlan(
    PreprocessingSettings Settings,
    DateTimeOffset RunStartedAtUtc,
    IReadOnlyList<PlannedFileOperation> Operations);
