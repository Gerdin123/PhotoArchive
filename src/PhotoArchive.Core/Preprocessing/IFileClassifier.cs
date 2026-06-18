namespace PhotoArchive.Core.Preprocessing;

public interface IFileClassifier
{
    Task<MediaClassification> ClassifyAsync(ScannedFile file, CancellationToken cancellationToken = default);
}
