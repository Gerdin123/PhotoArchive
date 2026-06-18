namespace PhotoArchive.App.Review;

public sealed record DirectorySetupResult(
    bool Preprocessed,
    int FileCount,
    string DatabasePath,
    string OutputRoot,
    string Message);
