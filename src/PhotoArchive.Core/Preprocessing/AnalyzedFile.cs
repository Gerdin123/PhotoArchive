using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed record AnalyzedFile(
    ScannedFile ScannedFile,
    MediaKind MediaKind,
    string Sha256Hash,
    DateInferenceEvidence DateEvidence,
    DateInferenceResult DateInference);
