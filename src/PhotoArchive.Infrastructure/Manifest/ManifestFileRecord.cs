using PhotoArchive.Core.Domain;

namespace PhotoArchive.Infrastructure.Manifest;

public sealed record ManifestFileRecord(
    string SourcePath,
    string PlannedDestination,
    string Sha256Hash,
    MediaKind MediaKind,
    DateTimeOffset? InferredDate,
    DateConfidence DateConfidence,
    string DateSource,
    bool IsDuplicate,
    string? DuplicateGroupId,
    string? CanonicalSourcePath,
    string ExecutionResult,
    string? Error);
