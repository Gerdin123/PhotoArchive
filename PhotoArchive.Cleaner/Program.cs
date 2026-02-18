using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;
using System.Diagnostics;
using System.Globalization;

namespace PhotoArchive.Cleaner
{
    internal class Program
    {
        private static readonly HashSet<string> LegacyProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".db", ".info", ".dat", ".pe4", ".idx"
        };

        private static readonly string[] ThumbnailMarkers =
        [
            "thumb", "thumbnail", "preview", "thm"
        ];

        static void Main(string[] args)
        {
            // Accept folder from CLI arg (preferred for automation) or prompt fallback.
            var sourcePath = ResolveSourcePath(args);
            if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
            {
                Console.WriteLine("The provided folder does not exist.");
                return;
            }

            // Never touch source files: write all output to a sibling cleaned folder.
            var outputRoot = CreateOutputFolder(sourcePath);
            Console.WriteLine($"Source: {sourcePath}");
            Console.WriteLine($"Output: {outputRoot}");
            var useFolderDate = ResolveFolderDatePreference(args);
            var groupThumbnails = ResolveThumbnailsPreference(args);
            var groupLegacyProgramFiles = ResolveLegacyProgramFilesPreference(args);
            Console.WriteLine($"Date source preference: {(useFolderDate ? "FolderNamePrefix -> EXIF -> FileCreationTime" : "EXIF -> FileCreationTime")}");
            Console.WriteLine($"Others/Thumbnails enabled: {groupThumbnails}");
            Console.WriteLine($"Others/OldProgramSpecific enabled: {groupLegacyProgramFiles}");

            // Lightweight manual composition. This is simple now and can be replaced by DI later.
            IFileScanner scanner = new FileScanner();
            IFileClassifier classifier = new FileClassifier();
            IMetadataExtractor metadataExtractor = new ExifMetadataExtractor();
            IDuplicateDetector duplicateDetector = new DuplicateDetector();
            IFileMover mover = new FileMover(sourcePath, outputRoot);
            IReportService report = new ReportService();
            var processedRecords = new List<ProcessedFileRecord>();
            var totalStopwatch = Stopwatch.StartNew();
            var files = scanner.ScanRecursively(sourcePath).ToList();
            var totalFiles = files.Count;
            Console.WriteLine($"Files discovered: {totalFiles}");
            if (totalFiles == 0)
            {
                totalStopwatch.Stop();
                report.PrintSummary();
                var emptyManifestPath = CsvExportService.Export(outputRoot, processedRecords);
                Console.WriteLine($"CSV manifest created: {emptyManifestPath}");
                Console.WriteLine($"Total time: {FormatDuration(totalStopwatch.Elapsed)}");
                Console.WriteLine("Metadata is preserved because files are copied byte-for-byte without modification.");
                return;
            }

            var analyzedFiles = new List<AnalyzedFile>(totalFiles);
            var progressTotal = totalFiles * 2;
            var progressCurrent = 0;

            foreach (var file in files)
            {
                try
                {
                    var fileType = classifier.Classify(file);
                    var fileInfo = new FileInfo(file);
                    var createdUtc = fileInfo.CreationTimeUtc;
                    var hash = duplicateDetector.ComputeHash(file);

                    // Grouping date priority:
                    // 1) Folder name date prefix yyyyMMdd when enabled and available
                    // 2) EXIF DateTaken (when available)
                    // 3) File creation time fallback
                    var hasDateTaken = metadataExtractor.TryGetDateTaken(file, out var dateTaken);
                    var fallbackDate = hasDateTaken ? dateTaken : fileInfo.CreationTime;
                    var folderDate = default(DateTime);
                    var folderDateSource = string.Empty;
                    var hasFolderDate = useFolderDate && TryGetDateFromFolder(sourcePath, file, fallbackDate, out folderDate, out folderDateSource);
                    var groupingDate = hasFolderDate ? folderDate : fallbackDate;
                    var groupingDateSource = hasFolderDate ? folderDateSource : (hasDateTaken ? "DateTaken" : "FileCreationTime");
                    analyzedFiles.Add(new AnalyzedFile
                    {
                        SourcePath = file,
                        FileType = fileType,
                        GroupingDate = groupingDate,
                        GroupingDateSource = groupingDateSource,
                        DateTaken = hasDateTaken ? dateTaken : null,
                        CreatedAtUtc = createdUtc,
                        LastWriteAtUtc = fileInfo.LastWriteTimeUtc,
                        SizeBytes = fileInfo.Length,
                        Extension = fileInfo.Extension,
                        Sha256 = hash
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping file '{file}'. Reason: {ex.Message}");
                }

                progressCurrent++;
                RenderProgress(progressCurrent, progressTotal, totalStopwatch.Elapsed, "Analyzing");
            }

            var canonicalByHash = analyzedFiles
                .GroupBy(x => x.Sha256, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(x => x.GroupingDate)
                        .ThenBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase)
                        .First()
                        .SourcePath,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var file in analyzedFiles)
            {
                var canonicalPath = canonicalByHash[file.Sha256];
                var isDuplicate = !string.Equals(file.SourcePath, canonicalPath, StringComparison.OrdinalIgnoreCase);
                var groupingYear = file.GroupingDate.Year;
                var groupingMonth = file.GroupingDate.Month;

                var bucket = "Others";
                string outputPath;

                if (isDuplicate)
                {
                    outputPath = mover.MoveToDuplicates(file.SourcePath);
                    bucket = "Duplicates";
                    report.RegisterDuplicate(file.SourcePath);
                }
                else if (file.FileType == FileType.Image)
                {
                    outputPath = mover.MoveToImages(file.SourcePath, groupingYear, groupingMonth);
                    bucket = "Images";
                    report.RegisterImage(file.SourcePath);
                }
                else
                {
                    if (groupThumbnails && IsThumbnailFile(file.SourcePath))
                    {
                        outputPath = mover.MoveToOthersCategory(file.SourcePath, "Thumbnails");
                        bucket = "Others/Thumbnails";
                    }
                    else if (groupLegacyProgramFiles && LegacyProgramExtensions.Contains(file.Extension))
                    {
                        outputPath = mover.MoveToOthersCategory(file.SourcePath, "OldProgramSpecific");
                        bucket = "Others/OldProgramSpecific";
                    }
                    else
                    {
                        outputPath = mover.MoveToOthers(file.SourcePath, groupingYear, groupingMonth);
                    }

                    report.RegisterOther(file.SourcePath);
                }

                processedRecords.Add(new ProcessedFileRecord
                {
                    SourcePath = file.SourcePath,
                    OutputPath = outputPath,
                    Bucket = bucket,
                    GroupingYear = groupingYear,
                    GroupingDateSource = file.GroupingDateSource,
                    GroupingDate = file.GroupingDate,
                    DateTaken = file.DateTaken,
                    CreatedYear = file.CreatedAtUtc.Year,
                    CreatedAtUtc = file.CreatedAtUtc,
                    LastWriteAtUtc = file.LastWriteAtUtc,
                    SizeBytes = file.SizeBytes,
                    Extension = file.Extension,
                    Sha256 = file.Sha256,
                    IsDuplicate = isDuplicate,
                    CanonicalSourcePath = isDuplicate ? canonicalPath : string.Empty
                });

                progressCurrent++;
                RenderProgress(progressCurrent, progressTotal, totalStopwatch.Elapsed, "Copying");
            }

            totalStopwatch.Stop();
            Console.WriteLine();
            report.PrintSummary();
            // One CSV row per processed file; this is the seed input for later API/database import.
            var manifestPath = CsvExportService.Export(outputRoot, processedRecords);
            Console.WriteLine($"CSV manifest created: {manifestPath}");
            Console.WriteLine($"Total time: {FormatDuration(totalStopwatch.Elapsed)}");
            Console.WriteLine("Metadata is preserved because files are copied byte-for-byte without modification.");
        }

        private static string ResolveSourcePath(string[] args)
        {
            if (args.Length > 0)
            {
                return Path.GetFullPath(args[0]);
            }

            Console.Write("Enter folder path to clean: ");
            var input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input.Trim());
        }

        private static bool ResolveFolderDatePreference(string[] args)
        {
            return ResolveBooleanPreference(
                args,
                "--date-from-folder",
                "Is Date based on folder structure? [Y/n]: ",
                defaultValue: true);
        }

        private static bool ResolveThumbnailsPreference(string[] args)
        {
            return ResolveBooleanPreference(
                args,
                "--others-thumbnails-folder",
                "Put thumbnail files in Others/Thumbnails? [Y/n]: ",
                defaultValue: true);
        }

        private static bool ResolveLegacyProgramFilesPreference(string[] args)
        {
            return ResolveBooleanPreference(
                args,
                "--others-legacy-folder",
                "Put .db/.info/.dat/.pe4/.idx files in Others/OldProgramSpecific? [Y/n]: ",
                defaultValue: true);
        }

        private static bool ResolveBooleanPreference(string[] args, string optionName, string prompt, bool defaultValue)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && TryParseBooleanOption(args[i + 1], out var nextValue))
                    {
                        return nextValue;
                    }

                    return true;
                }

                if (arg.StartsWith($"{optionName}=", StringComparison.OrdinalIgnoreCase))
                {
                    var valueText = arg[(optionName.Length + 1)..];
                    if (TryParseBooleanOption(valueText, out var inlineValue))
                    {
                        return inlineValue;
                    }
                }
            }

            Console.Write(prompt);
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }

            return TryParseBooleanOption(input, out var parsed) ? parsed : defaultValue;
        }

        private static bool TryParseBooleanOption(string value, out bool parsed)
        {
            var normalized = value.Trim();
            if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
            {
                parsed = true;
                return true;
            }

            if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                parsed = false;
                return true;
            }

            parsed = false;
            return false;
        }

        private static bool TryGetDateFromFolder(
            string sourceRoot,
            string filePath,
            DateTime fallbackDate,
            out DateTime parsedDate,
            out string source)
        {
            parsedDate = default;
            source = string.Empty;
            var sourceRootFullPath = Path.GetFullPath(sourceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            var current = new DirectoryInfo(directoryPath);
            while (current != null)
            {
                if (TryParseDatePrefix(current.Name, out var partialDate))
                {
                    parsedDate = ComposeDateFromPartial(partialDate, fallbackDate);
                    source = $"FolderNamePrefix({partialDate.Pattern})";
                    return true;
                }

                var currentFullPath = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (currentFullPath.Equals(sourceRootFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = current.Parent;
            }

            return false;
        }

        private static bool TryParseDatePrefix(string folderName, out PartialFolderDate parsedDate)
        {
            parsedDate = default;
            if (folderName.Length >= 8 && TryParseLeadingDigits(folderName, 8, out var yyyymmdd))
            {
                if (DateTime.TryParseExact(
                    yyyymmdd,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var exactDate))
                {
                    parsedDate = new PartialFolderDate(exactDate.Year, exactDate.Month, exactDate.Day, "yyyyMMdd");
                    return true;
                }
            }

            if (folderName.Length >= 6 && TryParseLeadingDigits(folderName, 6, out var yyyymm))
            {
                if (DateTime.TryParseExact(
                    yyyymm,
                    "yyyyMM",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var yearMonthDate))
                {
                    parsedDate = new PartialFolderDate(yearMonthDate.Year, yearMonthDate.Month, null, "yyyyMM");
                    return true;
                }
            }

            if (folderName.Length >= 4 && TryParseLeadingDigits(folderName, 4, out var yyyy))
            {
                if (int.TryParse(yyyy, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1000 and <= 9999)
                {
                    parsedDate = new PartialFolderDate(year, null, null, "yyyy");
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseLeadingDigits(string input, int count, out string digits)
        {
            digits = string.Empty;
            if (input.Length < count)
            {
                return false;
            }

            var prefix = input[..count];
            for (var i = 0; i < prefix.Length; i++)
            {
                if (!char.IsDigit(prefix[i]))
                {
                    return false;
                }
            }

            digits = prefix;
            return true;
        }

        private static DateTime ComposeDateFromPartial(PartialFolderDate folderDate, DateTime fallbackDate)
        {
            var month = folderDate.Month ?? fallbackDate.Month;
            month = Math.Clamp(month, 1, 12);

            var day = folderDate.Day ?? fallbackDate.Day;
            var maxDay = DateTime.DaysInMonth(folderDate.Year, month);
            day = Math.Clamp(day, 1, maxDay);

            return new DateTime(
                folderDate.Year,
                month,
                day,
                fallbackDate.Hour,
                fallbackDate.Minute,
                fallbackDate.Second,
                fallbackDate.Kind);
        }

        private static bool IsThumbnailFile(string filePath)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (ThumbnailMarkers.Any(marker => fileNameWithoutExtension.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            var segments = directoryPath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            return segments.Any(segment => ThumbnailMarkers.Any(marker => segment.Contains(marker, StringComparison.OrdinalIgnoreCase)));
        }

        private readonly record struct PartialFolderDate(int Year, int? Month, int? Day, string Pattern);

        private static string CreateOutputFolder(string sourcePath)
        {
            var sourceDirectory = new DirectoryInfo(sourcePath);
            var parentDirectory = sourceDirectory.Parent?.FullName ?? sourceDirectory.FullName;
            var baseName = string.IsNullOrWhiteSpace(sourceDirectory.Name) ? "root" : sourceDirectory.Name;

            var outputPath = Path.Combine(parentDirectory, $"{baseName}_cleaned");
            if (Directory.Exists(outputPath))
            {
                // Keep existing results intact by creating a timestamped folder instead of overwriting.
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                outputPath = Path.Combine(parentDirectory, $"{baseName}_cleaned_{timestamp}");
            }

            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static void RenderProgress(int processed, int total, TimeSpan elapsed, string activity)
        {
            const int barWidth = 30;
            var ratio = total == 0 ? 1d : (double)processed / total;
            var completed = (int)Math.Round(ratio * barWidth, MidpointRounding.AwayFromZero);
            completed = Math.Clamp(completed, 0, barWidth);
            var bar = new string('#', completed) + new string('-', barWidth - completed);
            var percent = ratio * 100;
            var eta = EstimateEta(processed, total, elapsed);
            var etaText = eta.HasValue ? FormatDuration(eta.Value) : "--:--";

            Console.Write($"\r[{bar}] {percent,6:0.00}%  {processed}/{total}  {activity,-9} Elapsed {FormatDuration(elapsed)}  ETA {etaText}");
        }

        private static TimeSpan? EstimateEta(int processed, int total, TimeSpan elapsed)
        {
            if (processed <= 0 || total <= 0 || processed >= total || elapsed.TotalMilliseconds <= 0)
            {
                return null;
            }

            var avgMsPerFile = elapsed.TotalMilliseconds / processed;
            var remaining = total - processed;
            return TimeSpan.FromMilliseconds(avgMsPerFile * remaining);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            }

            return duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private sealed class AnalyzedFile
        {
            public string SourcePath { get; init; } = string.Empty;
            public FileType FileType { get; init; }
            public DateTime GroupingDate { get; init; }
            public string GroupingDateSource { get; init; } = string.Empty;
            public DateTime? DateTaken { get; init; }
            public DateTime CreatedAtUtc { get; init; }
            public DateTime LastWriteAtUtc { get; init; }
            public long SizeBytes { get; init; }
            public string Extension { get; init; } = string.Empty;
            public string Sha256 { get; init; } = string.Empty;
        }
    }
}
