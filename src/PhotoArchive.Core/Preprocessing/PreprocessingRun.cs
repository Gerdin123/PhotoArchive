namespace PhotoArchive.Core.Preprocessing;

public sealed record PreprocessingRun(
    PreprocessingSettings Settings,
    DateTimeOffset RunStartedAtUtc,
    IReadOnlyList<AnalyzedFile> Files);
