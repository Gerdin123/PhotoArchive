using System.Security;
using System.Text;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure.Metadata;

public sealed class XmpSidecarMetadataWriter : IMetadataWriter
{
    public async Task WriteAsync(MetadataWriteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TakenDate is null)
        {
            throw new InvalidOperationException("Taken date is required for metadata write-back.");
        }

        var sidecarPath = GetSidecarPath(request.FilePath);
        var directory = Path.GetDirectoryName(sidecarPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var timestamp = request.TakenDate.Value.ToString("yyyy-MM-ddTHH:mm:sszzz");
        var escapedTimestamp = SecurityElement.Escape(timestamp);
        var content = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description rdf:about=""
                    xmlns:exif="http://ns.adobe.com/exif/1.0/"
                    xmlns:xmp="http://ns.adobe.com/xap/1.0/">
                  <exif:DateTimeOriginal>{{escapedTimestamp}}</exif:DateTimeOriginal>
                  <xmp:CreateDate>{{escapedTimestamp}}</xmp:CreateDate>
                  <xmp:MetadataDate>{{escapedTimestamp}}</xmp:MetadataDate>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """;

        await File.WriteAllTextAsync(sidecarPath, content, Encoding.UTF8, cancellationToken);
    }

    public static string GetSidecarPath(string filePath)
    {
        return Path.ChangeExtension(filePath, ".xmp");
    }
}
