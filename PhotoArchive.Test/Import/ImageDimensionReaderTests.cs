using PhotoArchive.Import;

namespace PhotoArchive.Test.Import;

public class ImageDimensionReaderTests
{
    [Fact]
    public void ResolveImageDimensions_ReadsPngDimensions()
    {
        var file = Path.Combine(Path.GetTempPath(), $"img-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(file, CreateMinimalPng(320, 240));

        try
        {
            var (width, height) = ImageDimensionReader.ResolveImageDimensions(file, string.Empty);

            Assert.Equal(320, width);
            Assert.Equal(240, height);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void ResolveImageDimensions_ReadsGifDimensions()
    {
        var file = Path.Combine(Path.GetTempPath(), $"img-{Guid.NewGuid():N}.gif");
        File.WriteAllBytes(file, CreateMinimalGif(64, 48));

        try
        {
            var (width, height) = ImageDimensionReader.ResolveImageDimensions(file, string.Empty);

            Assert.Equal(64, width);
            Assert.Equal(48, height);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    [Fact]
    public void ResolveImageDimensions_ReturnsNulls_WhenFilesMissing()
    {
        var result = ImageDimensionReader.ResolveImageDimensions("missing-a", "missing-b");

        Assert.Null(result.Width);
        Assert.Null(result.Height);
    }

    private static byte[] CreateMinimalPng(int width, int height)
    {
        var bytes = new byte[24];
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        Buffer.BlockCopy(signature, 0, bytes, 0, signature.Length);

        bytes[16] = (byte)((width >> 24) & 0xFF);
        bytes[17] = (byte)((width >> 16) & 0xFF);
        bytes[18] = (byte)((width >> 8) & 0xFF);
        bytes[19] = (byte)(width & 0xFF);

        bytes[20] = (byte)((height >> 24) & 0xFF);
        bytes[21] = (byte)((height >> 16) & 0xFF);
        bytes[22] = (byte)((height >> 8) & 0xFF);
        bytes[23] = (byte)(height & 0xFF);

        return bytes;
    }

    private static byte[] CreateMinimalGif(ushort width, ushort height)
    {
        var bytes = new byte[10];
        var header = System.Text.Encoding.ASCII.GetBytes("GIF89a");
        Buffer.BlockCopy(header, 0, bytes, 0, header.Length);
        bytes[6] = (byte)(width & 0xFF);
        bytes[7] = (byte)((width >> 8) & 0xFF);
        bytes[8] = (byte)(height & 0xFF);
        bytes[9] = (byte)((height >> 8) & 0xFF);
        return bytes;
    }
}
