namespace PhotoArchive.App.Review;

public sealed record DirectorySetupResult(
    bool Preprocessed,
    int FileCount,
    int ImagesLeft,
    int Duplicates,
    int UnsupportedFiles,
    TimeSpan Elapsed,
    string DatabasePath,
    string OutputRoot,
    string Message);
