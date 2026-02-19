using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.Import;

internal static class Program
{
    private const string ManifestFileName = "cleaned_manifest.csv";
    private const string DefaultDatabaseFileName = "photoarchive.db";

    private static int Main(string[] args)
    {
        var cleanedFolder = ResolveCleanedFolder(args);
        if (string.IsNullOrWhiteSpace(cleanedFolder) || !Directory.Exists(cleanedFolder))
        {
            Console.WriteLine("The provided cleaned folder does not exist.");
            return 1;
        }

        var manifestPath = Path.Combine(cleanedFolder, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"Could not find '{ManifestFileName}' in '{cleanedFolder}'.");
            return 1;
        }

        var databasePath = ResolveDatabasePath(args, cleanedFolder);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            Console.WriteLine("No database path was provided.");
            return 1;
        }

        if (!PrepareDatabaseLocation(args, databasePath))
        {
            return 1;
        }

        var dbOptions = new DbContextOptionsBuilder<PhotoArchiveDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        using var dbContext = new PhotoArchiveDbContext(dbOptions);
        dbContext.Database.EnsureCreated();

        var summary = ImportManifest(dbContext, manifestPath);

        Console.WriteLine();
        Console.WriteLine($"Manifest: {manifestPath}");
        Console.WriteLine($"Database: {databasePath}");
        Console.WriteLine($"Imported photos: {summary.Imported}");
        Console.WriteLine($"Skipped (non-images/duplicates): {summary.SkippedFiltered}");
        Console.WriteLine($"Skipped (duplicate hash within import): {summary.SkippedDuplicateHash}");
        Console.WriteLine($"Skipped (invalid rows): {summary.SkippedInvalid}");

        return 0;
    }

    private static ImportSummary ImportManifest(PhotoArchiveDbContext dbContext, string manifestPath)
    {
        using var reader = new StreamReader(manifestPath);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return new ImportSummary();
        }

        var headers = ParseCsvLine(headerLine);
        var indexes = BuildHeaderIndex(headers);

        var sourcePathIndex = GetRequiredIndex(indexes, "SourcePath");
        var outputPathIndex = GetRequiredIndex(indexes, "OutputPath");
        var bucketIndex = GetRequiredIndex(indexes, "Bucket");
        var groupingYearIndex = GetRequiredIndex(indexes, "GroupingYear");
        var groupingDateSourceIndex = GetRequiredIndex(indexes, "GroupingDateSource");
        var groupingDateIndex = GetRequiredIndex(indexes, "GroupingDate");
        var dateTakenIndex = GetRequiredIndex(indexes, "DateTaken");
        var createdAtUtcIndex = GetRequiredIndex(indexes, "CreatedAtUtc");
        var lastWriteAtUtcIndex = GetRequiredIndex(indexes, "LastWriteAtUtc");
        var sizeBytesIndex = GetRequiredIndex(indexes, "SizeBytes");
        var extensionIndex = GetRequiredIndex(indexes, "Extension");
        var sha256Index = GetRequiredIndex(indexes, "Sha256");
        var isDuplicateIndex = GetRequiredIndex(indexes, "IsDuplicate");
        var canonicalSourcePathIndex = GetRequiredIndex(indexes, "CanonicalSourcePath");

        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        var imported = 0;
        var skippedFiltered = 0;
        var skippedDuplicateHash = 0;
        var skippedInvalid = 0;
        var pending = 0;

        var importedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < headers.Count)
            {
                skippedInvalid++;
                Console.WriteLine($"Skipping line {lineNumber}: column count mismatch.");
                continue;
            }

            var bucket = GetField(fields, bucketIndex);
            var isDuplicate = ParseBoolean(GetField(fields, isDuplicateIndex));
            if (!bucket.Equals("Images", StringComparison.OrdinalIgnoreCase) || isDuplicate)
            {
                skippedFiltered++;
                continue;
            }

            var hash = GetField(fields, sha256Index);
            if (string.IsNullOrWhiteSpace(hash))
            {
                skippedInvalid++;
                Console.WriteLine($"Skipping line {lineNumber}: missing Sha256.");
                continue;
            }

            if (!importedHashes.Add(hash))
            {
                skippedDuplicateHash++;
                continue;
            }

            if (!TryCreatePhoto(fields,
                    sourcePathIndex,
                    outputPathIndex,
                    groupingYearIndex,
                    groupingDateSourceIndex,
                    groupingDateIndex,
                    dateTakenIndex,
                    createdAtUtcIndex,
                    lastWriteAtUtcIndex,
                    sizeBytesIndex,
                    extensionIndex,
                    sha256Index,
                    canonicalSourcePathIndex,
                    out var photo,
                    out var validationError))
            {
                skippedInvalid++;
                Console.WriteLine($"Skipping line {lineNumber}: {validationError}");
                continue;
            }

            dbContext.Photos.Add(photo);
            imported++;
            pending++;

            if (pending >= 500)
            {
                dbContext.SaveChanges();
                dbContext.ChangeTracker.Clear();
                pending = 0;
            }
        }

        if (pending > 0)
        {
            dbContext.SaveChanges();
        }

        return new ImportSummary
        {
            Imported = imported,
            SkippedFiltered = skippedFiltered,
            SkippedDuplicateHash = skippedDuplicateHash,
            SkippedInvalid = skippedInvalid
        };
    }

    private static bool TryCreatePhoto(
        IReadOnlyList<string> fields,
        int sourcePathIndex,
        int outputPathIndex,
        int groupingYearIndex,
        int groupingDateSourceIndex,
        int groupingDateIndex,
        int dateTakenIndex,
        int createdAtUtcIndex,
        int lastWriteAtUtcIndex,
        int sizeBytesIndex,
        int extensionIndex,
        int sha256Index,
        int canonicalSourcePathIndex,
        out Photo photo,
        out string error)
    {
        photo = null!;
        error = string.Empty;

        var sourcePath = GetField(fields, sourcePathIndex);
        var outputPath = GetField(fields, outputPathIndex);
        var extension = GetField(fields, extensionIndex);
        var sha256 = GetField(fields, sha256Index);

        if (!TryParseLong(GetField(fields, sizeBytesIndex), out var sizeBytes))
        {
            error = "invalid SizeBytes value.";
            return false;
        }

        if (!TryParseDateTime(GetField(fields, groupingDateIndex), out var groupingDate))
        {
            error = "invalid GroupingDate value.";
            return false;
        }

        if (!TryParseDateTime(GetField(fields, createdAtUtcIndex), out var createdAtUtc))
        {
            error = "invalid CreatedAtUtc value.";
            return false;
        }

        if (!TryParseDateTime(GetField(fields, lastWriteAtUtcIndex), out var lastWriteAtUtc))
        {
            error = "invalid LastWriteAtUtc value.";
            return false;
        }

        var groupingYearText = GetField(fields, groupingYearIndex);
        var groupingYear = TryParseInt(groupingYearText, out var parsedYear) ? parsedYear : groupingDate.Year;

        var groupingDateSource = ParseGroupingDateSource(GetField(fields, groupingDateSourceIndex));
        var dateTaken = TryParseNullableDateTime(GetField(fields, dateTakenIndex), out var parsedDateTaken)
            ? parsedDateTaken
            : null;

        var canonicalSourcePath = GetField(fields, canonicalSourcePathIndex);
        var fileName = Path.GetFileName(outputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(sourcePath);
        }
        var (width, height) = ResolveImageDimensions(outputPath, sourcePath);

        photo = new Photo
        {
            SourcePath = sourcePath,
            OutputPath = outputPath,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unknown" : fileName,
            Extension = string.IsNullOrWhiteSpace(extension) ? Path.GetExtension(outputPath) : extension,
            SizeBytes = sizeBytes,
            Sha256 = sha256,
            Bucket = PhotoBucket.Images,
            IsDuplicate = false,
            CanonicalSourcePath = string.IsNullOrWhiteSpace(canonicalSourcePath) ? null : canonicalSourcePath,
            GroupingYear = groupingYear,
            GroupingDateSource = groupingDateSource,
            GroupingDate = groupingDate,
            DateTaken = dateTaken,
            CreatedAtUtc = createdAtUtc,
            LastWriteAtUtc = lastWriteAtUtc,
            Width = width,
            Height = height
        };

        return true;
    }

    private static string ResolveCleanedFolder(string[] args)
    {
        if (TryGetOption(args, "--input", out var optionPath))
        {
            return Path.GetFullPath(optionPath);
        }

        var positional = GetPositionalArguments(args);
        if (positional.Count > 0)
        {
            return Path.GetFullPath(positional[0]);
        }

        Console.Write("Enter cleaned folder path: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input.Trim());
    }

    private static string ResolveDatabasePath(string[] args, string cleanedFolder)
    {
        if (TryGetOption(args, "--db", out var optionPath))
        {
            return NormalizeDatabasePath(optionPath, cleanedFolder);
        }

        var positional = GetPositionalArguments(args);
        if (positional.Count > 1)
        {
            return NormalizeDatabasePath(positional[1], cleanedFolder);
        }

        Console.Write("Database folder or file path (leave empty to use cleaned folder): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return NormalizeDatabasePath(cleanedFolder, cleanedFolder);
        }

        return NormalizeDatabasePath(input.Trim(), cleanedFolder);
    }

    private static string NormalizeDatabasePath(string inputPath, string cleanedFolder)
    {
        var candidate = string.IsNullOrWhiteSpace(inputPath) ? cleanedFolder : inputPath;
        var expanded = Path.GetFullPath(candidate);

        var extension = Path.GetExtension(expanded);
        if (IsDatabaseFileExtension(extension))
        {
            return expanded;
        }

        return Path.Combine(expanded, DefaultDatabaseFileName);
    }

    private static bool PrepareDatabaseLocation(string[] args, string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("Unable to resolve database directory.");
            return false;
        }

        Directory.CreateDirectory(directory);

        if (!File.Exists(databasePath))
        {
            return true;
        }

        var overwrite = ResolveOverwritePreference(args);
        if (!overwrite)
        {
            Console.WriteLine($"Database already exists: {databasePath}");
            Console.WriteLine("Import canceled. Use --overwrite true or choose another path.");
            return false;
        }

        File.Delete(databasePath);
        return true;
    }

    private static bool ResolveOverwritePreference(string[] args)
    {
        if (TryGetOption(args, "--overwrite", out var optionValue) && TryParseBoolean(optionValue, out var parsed))
        {
            return parsed;
        }

        Console.Write("Database exists. Overwrite? [Y/n]: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        return TryParseBoolean(input, out var interactive) ? interactive : true;
    }

    private static bool TryGetOption(string[] args, string optionName, out string value)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }
            }

            if (arg.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(optionName.Length + 1)..];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static List<string> GetPositionalArguments(string[] args)
    {
        var result = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                result.Add(arg);
                continue;
            }

            // Skip value token for known options in '--name value' form.
            if ((arg.Equals("--input", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--db", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--overwrite", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length
                && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                i++;
            }
        }

        return result;
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = headers[i].Trim().TrimStart('\uFEFF');
            if (!index.ContainsKey(normalized))
            {
                index[normalized] = i;
            }
        }

        return index;
    }

    private static int GetRequiredIndex(IReadOnlyDictionary<string, int> index, string key)
    {
        if (!index.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"The CSV is missing required column '{key}'.");
        }

        return value;
    }

    private static string GetField(IReadOnlyList<string> fields, int index)
    {
        if (index < 0 || index >= fields.Count)
        {
            return string.Empty;
        }

        return fields[index].Trim();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (ch == ',' && !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static bool ParseBoolean(string input)
    {
        return TryParseBoolean(input, out var value) && value;
    }

    private static bool TryParseBoolean(string input, out bool value)
    {
        var normalized = input.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("n", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryParseInt(string input, out int value)
    {
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseLong(string input, out long value)
    {
        return long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseDateTime(string input, out DateTime value)
    {
        return DateTime.TryParse(
            input,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
            out value);
    }

    private static bool TryParseNullableDateTime(string input, out DateTime? value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = null;
            return true;
        }

        if (TryParseDateTime(input, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static GroupingDateSource ParseGroupingDateSource(string input)
    {
        if (input.StartsWith("DateTaken", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.DateTaken;
        }

        if (input.StartsWith("FolderNamePrefix", StringComparison.OrdinalIgnoreCase))
        {
            return GroupingDateSource.FolderNamePrefix;
        }

        return GroupingDateSource.FileCreationTime;
    }

    private static (int? Width, int? Height) ResolveImageDimensions(string outputPath, string sourcePath)
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

                _ = reader.ReadByte(); // precision
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

    private static bool IsDatabaseFileExtension(string extension)
    {
        return extension.Equals(".db", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite3", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ImportSummary
    {
        public int Imported { get; init; }
        public int SkippedFiltered { get; init; }
        public int SkippedDuplicateHash { get; init; }
        public int SkippedInvalid { get; init; }
    }
}
