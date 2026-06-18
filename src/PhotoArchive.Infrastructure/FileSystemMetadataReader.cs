using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class FileSystemMetadataReader : IMetadataReader
{
    public Task<DateInferenceEvidence> ReadDateEvidenceAsync(
        ScannedFile file,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = new FileInfo(file.FullPath);
        return Task.FromResult(new DateInferenceEvidence(
            OriginalFileName: file.OriginalFileName,
            FileCreatedDate: info.CreationTimeUtc == DateTime.MinValue
                ? null
                : new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero),
            FileModifiedDate: info.LastWriteTimeUtc == DateTime.MinValue
                ? null
                : new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)));
    }
}
