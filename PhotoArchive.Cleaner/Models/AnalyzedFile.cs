namespace PhotoArchive.Cleaner.Models;

internal sealed class AnalyzedFile
{
    public string SourcePath { get; init; } = string.Empty;
    public FileType FileType { get; init; }
    public DateTime GroupingDate { get; init; }
    public string GroupingDateSource { get; init; } = string.Empty;
    public DateTime? DateTaken { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime LastWriteAtUtc { get; init; }
    public long SizeBytes { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
}
