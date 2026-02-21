using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class ExifMetadataExtractorTests
{
    [Fact]
    public void TryGetDateTaken_ReturnsFalse_ForUnsupportedExtension()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.png");
        File.WriteAllText(filePath, "not-an-image");

        try
        {
            var ok = extractor.TryGetDateTaken(filePath, out _);

            Assert.False(ok);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryGetDateTaken_ReturnsFalse_ForInvalidJpeg()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(filePath, [0xFF, 0xD8, 0xFF, 0xD9]);

        try
        {
            var ok = extractor.TryGetDateTaken(filePath, out _);

            Assert.False(ok);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryGetDateTaken_ReadsDate_FromMinimalTiff()
    {
        var extractor = new ExifMetadataExtractor();
        var filePath = Path.Combine(Path.GetTempPath(), $"photoarchive-exif-{Guid.NewGuid():N}.tif");
        File.WriteAllBytes(filePath, BuildMinimalTiffWithDateTime("2024:01:02 03:04:05"));

        try
        {
            var ok = extractor.TryGetDateTaken(filePath, out var dateTaken);

            Assert.True(ok);
            Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5), dateTaken);
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
}
