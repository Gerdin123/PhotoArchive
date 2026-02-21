namespace PhotoArchive.Import;

internal sealed record ImportOptions(
    string CleanedFolder,
    string ManifestPath,
    string DatabasePath);
