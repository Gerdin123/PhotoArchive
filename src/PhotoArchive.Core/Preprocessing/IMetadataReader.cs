namespace PhotoArchive.Core.Preprocessing;

public interface IMetadataReader
{
    Task<DateInferenceEvidence> ReadDateEvidenceAsync(ScannedFile file, CancellationToken cancellationToken = default);
}
