namespace PhotoArchive.Core.Preprocessing;

public sealed record DateInferenceEvidence(
    string OriginalFileName,
    DateTimeOffset? ExifDateTimeOriginal = null,
    DateTimeOffset? ExifCreateDate = null,
    DateTimeOffset? XmpDateCreated = null,
    DateTimeOffset? FileCreatedDate = null,
    DateTimeOffset? FileModifiedDate = null);
