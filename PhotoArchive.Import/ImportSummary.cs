namespace PhotoArchive.Import;

internal sealed class ImportSummary
{
    public int Imported { get; init; }
    public int SkippedFiltered { get; init; }
    public int SkippedDuplicateHash { get; init; }
    public int SkippedInvalid { get; init; }
}
