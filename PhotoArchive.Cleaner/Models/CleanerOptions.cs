namespace PhotoArchive.Cleaner.Models;

internal sealed record CleanerOptions(
    string SourcePath,
    string OutputRoot,
    bool GroupThumbnails,
    bool GroupLegacyProgramFiles);
