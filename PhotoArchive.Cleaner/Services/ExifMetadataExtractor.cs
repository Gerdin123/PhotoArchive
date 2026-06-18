using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    internal sealed class ExifMetadataExtractor : IMetadataExtractor
    {
        private const ushort TagImageWidth = 0x0100;
        private const ushort TagImageHeight = 0x0101;
        private const ushort TagMake = 0x010F;
        private const ushort TagModel = 0x0110;
        private const ushort TagOrientation = 0x0112;
        private const ushort TagModifyDate = 0x0132;
        private const ushort TagExifSubIfd = 0x8769;
        private const ushort TagXpKeywords = 0x9C9E;
        private const ushort TagXpSubject = 0x9C9F;
        private const ushort TagDateTimeOriginal = 0x9003;
        private const ushort TagCreateDate = 0x9004;
        private const ushort TagExifImageWidth = 0xA002;
        private const ushort TagExifImageHeight = 0xA003;

        private static readonly HashSet<string> ExifSupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".tif", ".tiff"
        };

        public bool TryExtract(string filePath, out ExtractedMetadata metadata)
        {
            metadata = new ExtractedMetadata();
            var extension = Path.GetExtension(filePath);
            if (!ExifSupportedExtensions.Contains(extension))
            {
                return false;
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                    ? TryReadFromJpeg(stream, out metadata)
                    : TryReadFromTiff(stream, out metadata);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadFromJpeg(Stream stream, out ExtractedMetadata metadata)
        {
            metadata = new ExtractedMetadata();
            var tags = new List<string>();
            Span<byte> header = stackalloc byte[2];
            Span<byte> lenBytes = stackalloc byte[2];
            if (stream.Read(header) != 2 || header[0] != 0xFF || header[1] != 0xD8)
            {
                return false;
            }

            var hasAnyMetadata = false;
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

                if (marker == -1)
                {
                    break;
                }

                if (marker == 0xD9 || marker == 0xDA)
                {
                    break;
                }

                if (stream.Read(lenBytes) != 2)
                    return false;

                var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);
                if (segmentLength < 2)
                    return false;

                var payloadLength = segmentLength - 2;
                if (payloadLength <= 0)
                {
                    continue;
                }

                var payload = new byte[payloadLength];
                if (stream.Read(payload, 0, payload.Length) != payload.Length)
                {
                    break;
                }

                if (marker == 0xE1 && payloadLength >= 6)
                {
                    if (payload[0] == (byte)'E' && payload[1] == (byte)'x' && payload[2] == (byte)'i' && payload[3] == (byte)'f' && payload[4] == 0x00 && payload[5] == 0x00)
                    {
                        using var exifStream = new MemoryStream(payload, 6, payload.Length - 6, writable: false);
                        if (TryReadFromTiff(exifStream, out var exifMetadata))
                        {
                            metadata = MergeMetadata(metadata, exifMetadata);
                            hasAnyMetadata = true;
                        }
                    }
                    else if (TryExtractXmpTags(payload, out var xmpTags))
                    {
                        tags.AddRange(xmpTags);
                        hasAnyMetadata = true;
                    }
                }
                else if (marker == 0xED && TryExtractIptcTags(payload, out var iptcTags))
                {
                    tags.AddRange(iptcTags);
                    hasAnyMetadata = true;
                }
                else
                {
                    continue;
                }
            }

            string[] mergedTags = [.. tags
                .Concat(metadata.ExifTags)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            metadata = new ExtractedMetadata
            {
                ExifDateTimeOriginal = metadata.ExifDateTimeOriginal,
                ExifCreateDate = metadata.ExifCreateDate,
                ExifModifyDate = metadata.ExifModifyDate,
                Width = metadata.Width,
                Height = metadata.Height,
                Orientation = metadata.Orientation,
                CameraMake = metadata.CameraMake,
                CameraModel = metadata.CameraModel,
                ExifTags = mergedTags
            };
            return hasAnyMetadata || metadata.ExifTags.Count > 0;
        }

        private static bool TryReadFromTiff(Stream stream, out ExtractedMetadata metadata)
        {
            metadata = new ExtractedMetadata();
            var tiffStart = stream.Position;

            Span<byte> endianBytes = stackalloc byte[2];
            if (stream.Read(endianBytes) != 2)
                return false;

            var littleEndian = endianBytes[0] == (byte)'I' && endianBytes[1] == (byte)'I';
            var bigEndian = endianBytes[0] == (byte)'M' && endianBytes[1] == (byte)'M';
            if (!littleEndian && !bigEndian)
                return false;

            Span<byte> versionBytes = stackalloc byte[2];
            if (stream.Read(versionBytes) != 2)
                return false;

            var version = ReadUInt16(versionBytes, littleEndian);
            if (version != 42)
                return false;

            var ifdOffset = ReadUInt32(stream, littleEndian);
            var exifOriginal = TryGetExifDate(stream, tiffStart, ifdOffset, littleEndian, TagDateTimeOriginal, out var originalDate)
                ? (DateTime?)originalDate
                : null;
            var exifCreate = TryGetExifDate(stream, tiffStart, ifdOffset, littleEndian, TagCreateDate, out var createDate)
                ? (DateTime?)createDate
                : null;
            var exifModify = TryGetExifDate(stream, tiffStart, ifdOffset, littleEndian, TagModifyDate, out var modifyDate)
                ? (DateTime?)modifyDate
                : null;

            var width = TryGetUIntValue(stream, tiffStart, ifdOffset, littleEndian, TagExifImageWidth, out var exifWidth)
                ? exifWidth
                : TryGetUIntValue(stream, tiffStart, ifdOffset, littleEndian, TagImageWidth, out var imageWidth) ? imageWidth : null;
            var height = TryGetUIntValue(stream, tiffStart, ifdOffset, littleEndian, TagExifImageHeight, out var exifHeight)
                ? exifHeight
                : TryGetUIntValue(stream, tiffStart, ifdOffset, littleEndian, TagImageHeight, out var imageHeight) ? imageHeight : null;

            var orientation = TryGetUIntValue(stream, tiffStart, ifdOffset, littleEndian, TagOrientation, out var orientationValue)
                ? orientationValue
                : null;
            var cameraMake = TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, TagMake, out var makeValue)
                ? makeValue
                : string.Empty;
            var cameraModel = TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, TagModel, out var modelValue)
                ? modelValue
                : string.Empty;
            var extractedTags = ExtractTags(stream, tiffStart, ifdOffset, littleEndian);

            metadata = new ExtractedMetadata
            {
                ExifDateTimeOriginal = exifOriginal,
                ExifCreateDate = exifCreate,
                ExifModifyDate = exifModify,
                Width = width,
                Height = height,
                Orientation = orientation,
                CameraMake = cameraMake,
                CameraModel = cameraModel,
                ExifTags = extractedTags
            };

            return metadata.ExifDateTimeOriginal.HasValue
                || metadata.ExifCreateDate.HasValue
                || metadata.ExifModifyDate.HasValue
                || metadata.Width.HasValue
                || metadata.Height.HasValue
                || metadata.Orientation.HasValue
                || !string.IsNullOrWhiteSpace(metadata.CameraMake)
                || !string.IsNullOrWhiteSpace(metadata.CameraModel)
                || metadata.ExifTags.Count > 0;
        }

        private static IReadOnlyList<string> ExtractTags(Stream stream, long tiffStart, uint ifdOffset, bool littleEndian)
        {
            var values = new List<string>();

            if (TryGetXpTagValue(stream, tiffStart, ifdOffset, littleEndian, TagXpKeywords, out var xpKeywords))
            {
                values.AddRange(SplitTagValues(xpKeywords));
            }

            if (TryGetXpTagValue(stream, tiffStart, ifdOffset, littleEndian, TagXpSubject, out var xpSubject))
            {
                values.AddRange(SplitTagValues(xpSubject));
            }

            return [.. values
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        private static bool TryGetXpTagValue(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            out string value)
        {
            value = string.Empty;
            if (!TryFindTag(stream, tiffStart, ifdOffset, littleEndian, targetTag, out var entry))
            {
                return false;
            }

            if (entry.Count == 0)
            {
                return false;
            }

            if (entry.Type is not 1 and not 7)
            {
                return false;
            }

            var rawBytes = ReadRawValueBytes(stream, tiffStart, entry.Count, entry.ValueOrOffset, littleEndian);
            if (rawBytes.Length == 0)
            {
                return false;
            }

            value = Encoding.Unicode.GetString(rawBytes).TrimEnd('\0', ' ');
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetExifDate(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            out DateTime parsedDate)
        {
            parsedDate = default;
            if (!TryGetAsciiTagValue(stream, tiffStart, ifdOffset, littleEndian, targetTag, out var rawDate))
            {
                return false;
            }

            return TryParseExifDate(rawDate, out parsedDate);
        }

        private static bool TryGetAsciiTagValue(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            out string value)
        {
            value = string.Empty;
            if (!TryFindTag(stream, tiffStart, ifdOffset, littleEndian, targetTag, out var entry))
            {
                return false;
            }

            if (entry.Type != 2 || entry.Count == 0)
            {
                return false;
            }

            value = ReadAsciiValue(stream, tiffStart, entry.Count, entry.ValueOrOffset, littleEndian);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetUIntValue(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            out int? value)
        {
            value = null;
            if (!TryFindTag(stream, tiffStart, ifdOffset, littleEndian, targetTag, out var entry))
            {
                return false;
            }

            if (entry.Count == 0)
            {
                return false;
            }

            if (entry.Type == 3)
            {
                value = ReadShortValue(entry.ValueOrOffset, littleEndian);
                return true;
            }

            if (entry.Type == 4)
            {
                value = checked((int)entry.ValueOrOffset);
                return true;
            }

            return false;
        }

        private static bool TryFindTag(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            out TiffEntry entry)
        {
            entry = default;
            var visited = new HashSet<uint>();
            return TryFindTagRecursive(stream, tiffStart, ifdOffset, littleEndian, targetTag, visited, out entry);
        }

        private static bool TryFindTagRecursive(
            Stream stream,
            long tiffStart,
            uint ifdOffset,
            bool littleEndian,
            ushort targetTag,
            ISet<uint> visitedOffsets,
            out TiffEntry entry)
        {
            entry = default;
            if (ifdOffset == 0 || !visitedOffsets.Add(ifdOffset))
            {
                return false;
            }

            stream.Position = tiffStart + ifdOffset;
            var entryCount = ReadUInt16(stream, littleEndian);

            uint? exifIfdOffset = null;
            for (var i = 0; i < entryCount; i++)
            {
                var tag = ReadUInt16(stream, littleEndian);
                var type = ReadUInt16(stream, littleEndian);
                var count = ReadUInt32(stream, littleEndian);
                var valueOrOffset = ReadUInt32(stream, littleEndian);

                if (tag == targetTag)
                {
                    entry = new TiffEntry(tag, type, count, valueOrOffset);
                    return true;
                }

                if (tag == TagExifSubIfd)
                {
                    exifIfdOffset = valueOrOffset;
                }
            }

            var nextIfdOffset = ReadUInt32(stream, littleEndian);
            if (exifIfdOffset.HasValue
                && TryFindTagRecursive(stream, tiffStart, exifIfdOffset.Value, littleEndian, targetTag, visitedOffsets, out entry))
            {
                return true;
            }

            if (nextIfdOffset != 0
                && TryFindTagRecursive(stream, tiffStart, nextIfdOffset, littleEndian, targetTag, visitedOffsets, out entry))
            {
                return true;
            }

            return false;
        }

        private static int ReadShortValue(uint raw, bool littleEndian)
        {
            Span<byte> bytes = stackalloc byte[4];
            if (littleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, raw);
                return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            }

            BinaryPrimitives.WriteUInt32BigEndian(bytes, raw);
            return BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        private static string ReadAsciiValue(Stream stream, long tiffStart, uint count, uint valueOrOffset, bool littleEndian)
        {
            if (count == 0)
            {
                return string.Empty;
            }

            var bytes = ReadRawValueBytes(stream, tiffStart, count, valueOrOffset, littleEndian);
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            return Encoding.ASCII.GetString(bytes).TrimEnd('\0', ' ');
        }

        private static byte[] ReadRawValueBytes(Stream stream, long tiffStart, uint count, uint valueOrOffset, bool littleEndian)
        {
            if (count == 0)
            {
                return [];
            }

            var bytes = new byte[count];
            if (count <= 4)
            {
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
                return bytes;
            }

            stream.Position = tiffStart + valueOrOffset;
            return stream.Read(bytes, 0, bytes.Length) == bytes.Length ? bytes : [];
        }

        private static IEnumerable<string> SplitTagValues(string rawValue)
        {
            return rawValue
                .Split([';', ',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static bool TryExtractXmpTags(byte[] payload, out IReadOnlyList<string> tags)
        {
            tags = [];

            var header = "http://ns.adobe.com/xap/1.0/\0"u8.ToArray();
            var xmlStart = 0;
            if (payload.Length >= header.Length && payload.AsSpan(0, header.Length).SequenceEqual(header))
            {
                xmlStart = header.Length;
            }
            else
            {
                var marker = Encoding.UTF8.GetBytes("<x:xmpmeta");
                var markerIndex = IndexOf(payload, marker);
                if (markerIndex < 0)
                {
                    return false;
                }

                xmlStart = markerIndex;
            }

            try
            {
                var xmlText = Encoding.UTF8.GetString(payload, xmlStart, payload.Length - xmlStart).Trim('\0', ' ', '\r', '\n', '\t');
                if (string.IsNullOrWhiteSpace(xmlText))
                {
                    return false;
                }

                var document = XDocument.Parse(xmlText, LoadOptions.None);
                var candidates = new List<string>();

                var subjectElements = document
                    .Descendants()
                    .Where(x => x.Name.LocalName.Equals("subject", StringComparison.OrdinalIgnoreCase));
                foreach (var subject in subjectElements)
                {
                    var liNodes = subject.Descendants().Where(x => x.Name.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase));
                    foreach (var li in liNodes)
                    {
                        candidates.Add(li.Value);
                    }
                }

                foreach (var keywordElement in document.Descendants().Where(x =>
                             x.Name.LocalName.Equals("Keywords", StringComparison.OrdinalIgnoreCase)
                             || x.Name.LocalName.Equals("Keyword", StringComparison.OrdinalIgnoreCase)
                             || x.Name.LocalName.Equals("Subject", StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(keywordElement.Value);
                }

                tags = [.. candidates
                    .SelectMany(SplitTagValues)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
                return tags.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryExtractIptcTags(byte[] payload, out IReadOnlyList<string> tags)
        {
            tags = [];
            if (payload.Length == 0)
            {
                return false;
            }

            var extracted = new List<string>();
            var i = 0;
            while (i + 5 < payload.Length)
            {
                if (payload[i] != 0x1C)
                {
                    i++;
                    continue;
                }

                var record = payload[i + 1];
                var dataset = payload[i + 2];
                var length = (payload[i + 3] << 8) | payload[i + 4];
                i += 5;
                if (length < 0 || i + length > payload.Length)
                {
                    break;
                }

                if (record == 0x02 && dataset == 0x19)
                {
                    var valueBytes = payload.AsSpan(i, length).ToArray();
                    var value = DecodeBestEffortText(valueBytes);
                    extracted.AddRange(SplitTagValues(value));
                }

                i += length;
            }

            tags = [.. extracted
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
            return tags.Count > 0;
        }

        private static string DecodeBestEffortText(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            var utf8 = Encoding.UTF8.GetString(bytes);
            if (!utf8.Contains('\uFFFD'))
            {
                return utf8;
            }

            return Encoding.Latin1.GetString(bytes);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length)
            {
                return -1;
            }

            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                {
                    return i;
                }
            }

            return -1;
        }

        private static ExtractedMetadata MergeMetadata(ExtractedMetadata current, ExtractedMetadata incoming)
        {
            return new ExtractedMetadata
            {
                ExifDateTimeOriginal = current.ExifDateTimeOriginal ?? incoming.ExifDateTimeOriginal,
                ExifCreateDate = current.ExifCreateDate ?? incoming.ExifCreateDate,
                ExifModifyDate = current.ExifModifyDate ?? incoming.ExifModifyDate,
                Width = current.Width ?? incoming.Width,
                Height = current.Height ?? incoming.Height,
                Orientation = current.Orientation ?? incoming.Orientation,
                CameraMake = string.IsNullOrWhiteSpace(current.CameraMake) ? incoming.CameraMake : current.CameraMake,
                CameraModel = string.IsNullOrWhiteSpace(current.CameraModel) ? incoming.CameraModel : current.CameraModel,
                ExifTags = [.. current.ExifTags
                    .Concat(incoming.ExifTags)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)]
            };
        }

        private static bool TryParseExifDate(string value, out DateTime parsed)
        {
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

        private readonly record struct TiffEntry(ushort Tag, ushort Type, uint Count, uint ValueOrOffset);
    }
}
