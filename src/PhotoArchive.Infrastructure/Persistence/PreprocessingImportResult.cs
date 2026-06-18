namespace PhotoArchive.Infrastructure.Persistence;

public sealed record PreprocessingImportResult(
    int ArchiveFiles,
    int MetadataRows,
    int DuplicateGroups,
    int OperationLogs);
