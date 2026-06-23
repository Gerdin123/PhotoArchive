using System.Security;
using System.Text;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure.Metadata;

public sealed class EmbeddedXmpMetadataWriter : IMetadataWriter
{
    private static readonly byte[] XmpHeader = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");

    public async Task WriteAsync(MetadataWriteRequest request, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(request.FilePath);
        if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Embedded XMP write-back currently supports JPEG files only.");
        }

        var bytes = await File.ReadAllBytesAsync(request.FilePath, cancellationToken);
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8)
        {
            throw new InvalidOperationException("The target file is not a valid JPEG image.");
        }

        var packet = BuildXmpPacket(request);
        if (packet.Length + 2 > ushort.MaxValue)
        {
            throw new InvalidOperationException("The XMP metadata packet is too large for a JPEG APP1 segment.");
        }

        var cleaned = RemoveExistingXmpSegments(bytes);
        var output = InsertXmpSegment(cleaned, packet);
        await File.WriteAllBytesAsync(request.FilePath, output, cancellationToken);
    }

    public static string BuildXmpPacket(MetadataWriteRequest request)
    {
        var titleXml = string.IsNullOrWhiteSpace(request.Title)
            ? string.Empty
            : $$"""
                  <dc:title>
                    <rdf:Alt>
                      <rdf:li xml:lang="x-default">{{SecurityElement.Escape(request.Title.Trim())}}</rdf:li>
                    </rdf:Alt>
                  </dc:title>
            """;
        var tags = request.Tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        var subjectXml = tags.Length == 0
            ? string.Empty
            : $$"""
                  <dc:subject>
                    <rdf:Bag>
            {{string.Join(Environment.NewLine, tags.Select(tag => $"          <rdf:li>{SecurityElement.Escape(tag)}</rdf:li>"))}}
                    </rdf:Bag>
                  </dc:subject>
            """;
        var dateXml = request.TakenDate is null
            ? string.Empty
            : $$"""
                  <exif:DateTimeOriginal>{{SecurityElement.Escape(request.TakenDate.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"))}}</exif:DateTimeOriginal>
                  <xmp:CreateDate>{{SecurityElement.Escape(request.TakenDate.Value.ToString("yyyy-MM-ddTHH:mm:sszzz"))}}</xmp:CreateDate>
            """;

        return $$"""
            <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description rdf:about=""
                    xmlns:dc="http://purl.org/dc/elements/1.1/"
                    xmlns:exif="http://ns.adobe.com/exif/1.0/"
                    xmlns:xmp="http://ns.adobe.com/xap/1.0/">
            {{dateXml}}
            {{titleXml}}
            {{subjectXml}}
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            <?xpacket end="w"?>
            """;
    }

    private static byte[] RemoveExistingXmpSegments(byte[] bytes)
    {
        var output = new List<byte>(bytes.Length);
        output.Add(bytes[0]);
        output.Add(bytes[1]);
        var index = 2;
        while (index + 4 <= bytes.Length && bytes[index] == 0xff)
        {
            var marker = bytes[index + 1];
            if (marker == 0xda || marker == 0xd9)
            {
                break;
            }

            if (marker is >= 0xd0 and <= 0xd7 or 0x01)
            {
                output.Add(bytes[index]);
                output.Add(bytes[index + 1]);
                index += 2;
                continue;
            }

            var segmentLength = (bytes[index + 2] << 8) + bytes[index + 3];
            if (segmentLength < 2 || index + 2 + segmentLength > bytes.Length)
            {
                break;
            }

            var payloadOffset = index + 4;
            var payloadLength = segmentLength - 2;
            var isXmp = marker == 0xe1
                && payloadLength >= XmpHeader.Length
                && bytes.AsSpan(payloadOffset, XmpHeader.Length).SequenceEqual(XmpHeader);

            if (!isXmp)
            {
                output.AddRange(bytes.AsSpan(index, 2 + segmentLength).ToArray());
            }

            index += 2 + segmentLength;
        }

        output.AddRange(bytes.AsSpan(index).ToArray());
        return output.ToArray();
    }

    private static byte[] InsertXmpSegment(byte[] jpegBytes, string xmpPacket)
    {
        var packetBytes = Encoding.UTF8.GetBytes(xmpPacket);
        var segmentLength = XmpHeader.Length + packetBytes.Length + 2;
        var output = new List<byte>(jpegBytes.Length + segmentLength + 2)
        {
            jpegBytes[0],
            jpegBytes[1],
            0xff,
            0xe1,
            (byte)(segmentLength >> 8),
            (byte)(segmentLength & 0xff)
        };
        output.AddRange(XmpHeader);
        output.AddRange(packetBytes);
        output.AddRange(jpegBytes.AsSpan(2).ToArray());
        return output.ToArray();
    }
}
