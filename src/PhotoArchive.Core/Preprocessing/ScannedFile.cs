namespace PhotoArchive.Core.Preprocessing;

public sealed record ScannedFile(
    string FullPath,
    string OriginalFileName,
    string Extension,
    long FileSizeBytes);
