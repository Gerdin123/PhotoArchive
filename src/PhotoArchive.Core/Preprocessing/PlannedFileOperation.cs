using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed record PlannedFileOperation(
    string SourcePath,
    string DestinationPath,
    MediaKind MediaKind,
    string Sha256Hash,
    DateTimeOffset? InferredTakenDate,
    DateConfidence DateConfidence,
    string DateSource,
    bool IsDuplicate,
    string? CanonicalSourcePath,
    string? DuplicateGroupId,
    string ExecutionResult = "Planned",
    string? ErrorMessage = null);
