namespace PhotoArchive.Import;

internal static class ImageDimensionReader
{
    public static (int? Width, int? Height) ResolveImageDimensions(string outputPath, string sourcePath)
    {
        if (TryReadImageDimensions(outputPath, out var outputDimensions))
        {
            return outputDimensions;
        }

        if (TryReadImageDimensions(sourcePath, out var sourceDimensions))
        {
            return sourceDimensions;
        }

        return (null, null);
    }

    private static bool TryReadImageDimensions(string path, out (int? Width, int? Height) dimensions)
    {
        dimensions = (null, null);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (TryReadPngDimensions(reader, out dimensions)
                || TryReadGifDimensions(reader, out dimensions)
                || TryReadBmpDimensions(reader, out dimensions)
                || TryReadJpegDimensions(reader, out dimensions))
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TryReadPngDimensions(BinaryReader reader, out (int? Width, int? Height) dimensions)
    {
        dimensions = (null, null);
        reader.BaseStream.Position = 0;
        if (reader.BaseStream.Length < 24)
        {
            return false;
        }

        var signature = reader.ReadBytes(8);
        var pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (!signature.SequenceEqual(pngSignature))
        {
            return false;
        }

        reader.BaseStream.Position = 16;
        var width = ReadInt32BigEndian(reader);
        var height = ReadInt32BigEndian(reader);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        dimensions = (width, height);
        return true;
    }

    private static bool TryReadGifDimensions(BinaryReader reader, out (int? Width, int? Height) dimensions)
    {
        dimensions = (null, null);
        reader.BaseStream.Position = 0;
        if (reader.BaseStream.Length < 10)
        {
            return false;
        }

        var header = reader.ReadBytes(6);
        var isGif = header.SequenceEqual("GIF87a"u8.ToArray()) || header.SequenceEqual("GIF89a"u8.ToArray());
        if (!isGif)
        {
            return false;
        }

        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        if (width == 0 || height == 0)
        {
            return false;
        }

        dimensions = (width, height);
        return true;
    }

    private static bool TryReadBmpDimensions(BinaryReader reader, out (int? Width, int? Height) dimensions)
    {
        dimensions = (null, null);
        reader.BaseStream.Position = 0;
        if (reader.BaseStream.Length < 26)
        {
            return false;
        }

        if (reader.ReadByte() != (byte)'B' || reader.ReadByte() != (byte)'M')
        {
            return false;
        }

        reader.BaseStream.Position = 18;
        var width = reader.ReadInt32();
        var height = Math.Abs(reader.ReadInt32());
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        dimensions = (width, height);
        return true;
    }

    private static bool TryReadJpegDimensions(BinaryReader reader, out (int? Width, int? Height) dimensions)
    {
        dimensions = (null, null);
        reader.BaseStream.Position = 0;
        if (reader.BaseStream.Length < 4)
        {
            return false;
        }

        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            return false;
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length - 1)
        {
            if (reader.ReadByte() != 0xFF)
            {
                continue;
            }

            var marker = reader.ReadByte();
            while (marker == 0xFF && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                marker = reader.ReadByte();
            }

            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            if (reader.BaseStream.Position + 2 > reader.BaseStream.Length)
            {
                return false;
            }

            var segmentLength = ReadUInt16BigEndian(reader);
            if (segmentLength < 2)
            {
                return false;
            }

            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (reader.BaseStream.Position + 5 > reader.BaseStream.Length)
                {
                    return false;
                }

                _ = reader.ReadByte();
                var height = ReadUInt16BigEndian(reader);
                var width = ReadUInt16BigEndian(reader);
                if (width == 0 || height == 0)
                {
                    return false;
                }

                dimensions = (width, height);
                return true;
            }

            var nextSegmentPosition = reader.BaseStream.Position + segmentLength - 2;
            if (nextSegmentPosition > reader.BaseStream.Length)
            {
                return false;
            }

            reader.BaseStream.Position = nextSegmentPosition;
        }

        return false;
    }

    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(sizeof(int));
        if (bytes.Length != sizeof(int))
        {
            return 0;
        }

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToInt32(bytes, 0);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(sizeof(ushort));
        if (bytes.Length != sizeof(ushort))
        {
            return 0;
        }

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt16(bytes, 0);
    }
}
