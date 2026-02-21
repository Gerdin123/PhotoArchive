namespace PhotoArchive.Import;

internal sealed record ManifestColumnIndexes(
    int SourcePath,
    int OutputPath,
    int Bucket,
    int GroupingYear,
    int GroupingDateSource,
    int GroupingDate,
    int DateTaken,
    int CreatedAtUtc,
    int LastWriteAtUtc,
    int SizeBytes,
    int Extension,
    int Sha256,
    int IsDuplicate,
    int CanonicalSourcePath)
{
    public static ManifestColumnIndexes FromHeaderIndex(IReadOnlyDictionary<string, int> index)
    {
        return new ManifestColumnIndexes(
            SourcePath: ManifestCsv.GetRequiredIndex(index, "SourcePath"),
            OutputPath: ManifestCsv.GetRequiredIndex(index, "OutputPath"),
            Bucket: ManifestCsv.GetRequiredIndex(index, "Bucket"),
            GroupingYear: ManifestCsv.GetRequiredIndex(index, "GroupingYear"),
            GroupingDateSource: ManifestCsv.GetRequiredIndex(index, "GroupingDateSource"),
            GroupingDate: ManifestCsv.GetRequiredIndex(index, "GroupingDate"),
            DateTaken: ManifestCsv.GetRequiredIndex(index, "DateTaken"),
            CreatedAtUtc: ManifestCsv.GetRequiredIndex(index, "CreatedAtUtc"),
            LastWriteAtUtc: ManifestCsv.GetRequiredIndex(index, "LastWriteAtUtc"),
            SizeBytes: ManifestCsv.GetRequiredIndex(index, "SizeBytes"),
            Extension: ManifestCsv.GetRequiredIndex(index, "Extension"),
            Sha256: ManifestCsv.GetRequiredIndex(index, "Sha256"),
            IsDuplicate: ManifestCsv.GetRequiredIndex(index, "IsDuplicate"),
            CanonicalSourcePath: ManifestCsv.GetRequiredIndex(index, "CanonicalSourcePath"));
    }
}
