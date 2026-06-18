using PhotoArchive.Cleaner.Services;
using System.Buffers.Binary;
using System.Text;

namespace PhotoArchive.Test.Cleaner;

public class ExifMetadataExtractorTests
{
    [Fact]
    public void TryExtract_ReturnsFalse_ForUnsupportedExtension()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.png");
        File.WriteAllText(filePath, "not-an-image");

        try
        {
            var ok = extractor.TryExtract(filePath, out _);

            Assert.False(ok);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryExtract_ReturnsFalse_ForInvalidJpeg()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(filePath, [0xFF, 0xD8, 0xFF, 0xD9]);

        try
        {
            var ok = extractor.TryExtract(filePath, out _);

            Assert.False(ok);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryExtract_ReadsDate_FromMinimalTiff()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.tif");
        File.WriteAllBytes(filePath, BuildMinimalTiffWithDateTime("2024:01:02 03:04:05"));

        try
        {
            var ok = extractor.TryExtract(filePath, out var metadata);

            Assert.True(ok);
            Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), metadata.ExifModifyDate);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryExtract_ReadsTags_FromJpegXmp()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(filePath, BuildMinimalJpegWithXmpTags("Köket", "Ulla Johansson", "Familjer", "Korsord"));

        try
        {
            var ok = extractor.TryExtract(filePath, out var metadata);

            Assert.True(ok);
            Assert.Contains("Köket", metadata.ExifTags);
            Assert.Contains("Ulla Johansson", metadata.ExifTags);
            Assert.Contains("Familjer", metadata.ExifTags);
            Assert.Contains("Korsord", metadata.ExifTags);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static byte[] BuildMinimalTiffWithDateTime(string exifDate)
    {
        var dateBytes = System.Text.Encoding.ASCII.GetBytes(exifDate + "\0");
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)'I');
        bw.Write((byte)'I');
        bw.Write((ushort)42);
        bw.Write((uint)8); // IFD starts immediately after TIFF header.

        bw.Write((ushort)1); // one entry
        bw.Write((ushort)0x0132); // DateTime tag
        bw.Write((ushort)2); // ASCII type
        bw.Write((uint)dateBytes.Length);
        bw.Write((uint)26); // value offset
        bw.Write((uint)0); // next IFD offset
        bw.Write(dateBytes);

        return ms.ToArray();
    }

    private static byte[] BuildMinimalJpegWithXmpTags(params string[] tags)
    {
        var li = string.Join(string.Empty, tags.Select(x => $"<rdf:li>{x}</rdf:li>"));
        var xmp =
            """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
              <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
                <rdf:Description xmlns:dc="http://purl.org/dc/elements/1.1/">
                  <dc:subject>
                    <rdf:Bag>
            """ +
            li +
            """
                    </rdf:Bag>
                  </dc:subject>
                </rdf:Description>
              </rdf:RDF>
            </x:xmpmeta>
            """;
        var header = "http://ns.adobe.com/xap/1.0/\0";
        var payload = Encoding.UTF8.GetBytes(header + xmp);

        using var ms = new MemoryStream();
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8); // SOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE1); // APP1

        Span<byte> lenBytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(lenBytes, checked((ushort)(payload.Length + 2)));
        ms.Write(lenBytes);
        ms.Write(payload);

        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9); // EOI
        return ms.ToArray();
    }
}
