using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed record MediaClassification(
    MediaKind MediaKind,
    string Reason);
