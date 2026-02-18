using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace PhotoArchive.Cleaner.Services
{
    internal sealed class ExifMetadataExtractor : IMetadataExtractor
    {
        // EXIF date parsing is currently implemented for these formats.
        private static readonly HashSet<string> ExifSupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".tif", ".tiff"
        };

        public bool TryGetDateTaken(string filePath, out DateTime dateTaken)
        {
            dateTaken = default;
            var extension = Path.GetExtension(filePath);
            if (!ExifSupportedExtensions.Contains(extension))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                // JPEG stores EXIF inside APP1 segments. TIFF stores EXIF directly in IFD structures.
                if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    return TryReadFromJpeg(stream, out dateTaken);
                }

                return TryReadFromTiff(stream, out dateTaken);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadFromJpeg(Stream stream, out DateTime dateTaken)
        {
            dateTaken = default;
            Span<byte> header = stackalloc byte[2];
            Span<byte> lenBytes = stackalloc byte[2];
            // JPEG SOI marker: FF D8.
            if (stream.Read(header) != 2 || header[0] != 0xFF || header[1] != 0xD8)
            {
                return false;
            }

            // Walk JPEG segments until we find APP1 EXIF block.
            while (stream.Position < stream.Length)
            {
                int prefix;
                do
                {
                    prefix = stream.ReadByte();
                } while (prefix != -1 && prefix != 0xFF);

                if (prefix == -1)
                    return false;

                int marker;
                do
                {
                    marker = stream.ReadByte();
                } while (marker == 0xFF);

                if (marker == -1 || marker == 0xD9 || marker == 0xDA)
                    return false;

                if (stream.Read(lenBytes) != 2)
                    return false;

                var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
                if (segmentLength < 2)
                    return false;

                var payloadLength = segmentLength - 2;
                if (marker == 0xE1 && payloadLength >= 6)
                {
                    var payload = new byte[payloadLength];
                    if (stream.Read(payload, 0, payload.Length) != payload.Length)
                        return false;

                    if (payload[0] == (byte)'E' && payload[1] == (byte)'x' && payload[2] == (byte)'i' && payload[3] == (byte)'f' && payload[4] == 0x00 && payload[5] == 0x00)
                    {
                        // After "Exif\0\0", payload is a TIFF blob.
                        using var exifStream = new MemoryStream(payload, 6, payload.Length - 6, writable: false);
                        return TryReadFromTiff(exifStream, out dateTaken);
                    }
                }
                else
                    // Skip non-EXIF payloads.
                    stream.Seek(payloadLength, SeekOrigin.Current);
            }

            return false;
        }

        private static bool TryReadFromTiff(Stream stream, out DateTime dateTaken)
        {
            dateTaken = default;
            var tiffStart = stream.Position;

            Span<byte> endianBytes = stackalloc byte[2];
            if (stream.Read(endianBytes) != 2)
                return false;

            bool littleEndian = endianBytes[0] == (byte)'I' && endianBytes[1] == (byte)'I';
            bool bigEndian = endianBytes[0] == (byte)'M' && endianBytes[1] == (byte)'M';
            if (!littleEndian && !bigEndian)
                return false;

            Span<byte> versionBytes = stackalloc byte[2];
            if (stream.Read(versionBytes) != 2)
                return false;

            var version = ReadUInt16(versionBytes, littleEndian);
            // TIFF fixed version identifier.
            if (version != 42)
                return false;

            var ifdOffset = ReadUInt32(stream, littleEndian);
            // Try common EXIF date tags in order of preference:
            // DateTimeOriginal (0x9003), DateTimeDigitized (0x9004), DateTime (0x0132).
            if (!TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, 0x9003, out var dateString)
                && !TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, 0x9004, out dateString)
                && !TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, 0x0132, out dateString))
            {
                return false;
            }

            return TryParseExifDate(dateString, out dateTaken);
        }

        private static bool TryGetAsciiTagValue(Stream stream, long tiffStart, uint ifdOffset, bool littleEndian, ushort targetTag, out string value)
        {
            value = string.Empty;
            if (ifdOffset == 0)
            {
                return false;
            }

            // IFD = image file directory (table of tags).
            stream.Position = tiffStart + ifdOffset;
            var entryCount = ReadUInt16(stream, littleEndian);

            uint? exifIfdOffset = null;

            for (var i = 0; i < entryCount; i++)
            {
                var tag = ReadUInt16(stream, littleEndian);
                var type = ReadUInt16(stream, littleEndian);
                var count = ReadUInt32(stream, littleEndian);
                var valueOrOffset = ReadUInt32(stream, littleEndian);

                if (tag == targetTag && type == 2 && count > 0)
                {
                    // TIFF type 2 is ASCII.
                    value = ReadAsciiValue(stream, tiffStart, count, valueOrOffset, littleEndian);
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (tag == 0x8769)
                {
                    // EXIF SubIFD pointer.
                    exifIfdOffset = valueOrOffset;
                }
            }

            if (exifIfdOffset.HasValue)
            {
                // Recursively inspect EXIF subdirectory for the requested tag.
                return TryGetAsciiTagValue(stream, tiffStart, exifIfdOffset.Value, littleEndian, targetTag, out value);
            }

            return false;
        }

        private static string ReadAsciiValue(Stream stream, long tiffStart, uint count, uint valueOrOffset, bool littleEndian)
        {
            if (count == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[count];
            if (count <= 4)
            {
                // TIFF packs short values directly into the 4-byte offset field.
                Span<byte> raw = stackalloc byte[4];
                if (littleEndian)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(raw, valueOrOffset);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32BigEndian(raw, valueOrOffset);
                }

                raw[..(int)count].CopyTo(bytes);
            }
            else
            {
                // Longer values are stored elsewhere; field contains byte offset.
                stream.Position = tiffStart + valueOrOffset;
                if (stream.Read(bytes, 0, bytes.Length) != bytes.Length)
                {
                    return string.Empty;
                }
            }

            var rawValue = Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
            return rawValue;
        }

        private static bool TryParseExifDate(string value, out DateTime parsed)
        {
            // EXIF date format uses colons in date component.
            return DateTime.TryParseExact(
                value,
                "yyyy:MM:dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed);
        }

        private static ushort ReadUInt16(Stream stream, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[2];
            if (stream.Read(bytes) != 2)
            {
                throw new EndOfStreamException();
            }

            return ReadUInt16(bytes, littleEndian);
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
        {
            return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        private static uint ReadUInt32(Stream stream, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[4];
            if (stream.Read(bytes) != 4)
            {
                throw new EndOfStreamException();
            }

            return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
        }
    }
}
