using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed record DateInferenceResult(
    DateTimeOffset? TakenDate,
    DateConfidence Confidence,
    string Source);
