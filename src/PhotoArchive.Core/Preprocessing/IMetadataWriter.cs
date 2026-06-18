namespace PhotoArchive.Core.Preprocessing;

public interface IMetadataWriter
{
    Task WriteAsync(MetadataWriteRequest request, CancellationToken cancellationToken = default);
}
