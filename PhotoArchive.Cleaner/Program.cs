using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Cleaner
{
    internal class Program
    {
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

            // Lightweight manual composition. This is simple now and can be replaced by DI later.
            IFileScanner scanner = new FileScanner();
            IFileClassifier classifier = new FileClassifier();
            IMetadataExtractor metadataExtractor = new ExifMetadataExtractor();
            IDuplicateDetector duplicateDetector = new DuplicateDetector();
            IFileMover mover = new FileMover(sourcePath, outputRoot);
            IReportService report = new ReportService();
            var processedRecords = new List<ProcessedFileRecord>();

            foreach (var file in scanner.ScanRecursively(sourcePath))
            {
                try
                {
                    // Classification is independent from duplicate detection.
                    // A duplicate image still goes to Duplicates.
                    var fileType = classifier.Classify(file);
                    var duplicateResult = duplicateDetector.Register(file);
                    var fileInfo = new FileInfo(file);
                    var createdUtc = fileInfo.CreationTimeUtc;

                    // Grouping date priority:
                    // 1) EXIF DateTaken (when available)
                    // 2) File creation time fallback
                    var hasDateTaken = metadataExtractor.TryGetDateTaken(file, out var dateTaken);
                    var groupingDate = hasDateTaken ? dateTaken : fileInfo.CreationTime;
                    var groupingDateSource = hasDateTaken ? "DateTaken" : "FileCreationTime";
                    var groupingYear = groupingDate.Year;

                    var bucket = "Others";
                    string outputPath;

                    if (duplicateResult.IsDuplicate)
                    {
                        outputPath = mover.MoveToDuplicates(file);
                        bucket = "Duplicates";
                        report.RegisterDuplicate(file);
                    }
                    else if (fileType == FileType.Image)
                    {
                        outputPath = mover.MoveToImages(file, groupingYear);
                        bucket = "Images";
                        report.RegisterImage(file);
                    }
                    else
                    {
                        outputPath = mover.MoveToOthers(file, groupingYear);
                        report.RegisterOther(file);
                    }

                    processedRecords.Add(new ProcessedFileRecord
                    {
                        // Source and destination are both stored to support later import/mapping.
                        SourcePath = file,
                        OutputPath = outputPath,
                        Bucket = bucket,
                        GroupingYear = groupingYear,
                        GroupingDateSource = groupingDateSource,
                        GroupingDate = groupingDate,
                        DateTaken = hasDateTaken ? dateTaken : null,
                        CreatedYear = createdUtc.Year,
                        CreatedAtUtc = createdUtc,
                        LastWriteAtUtc = fileInfo.LastWriteTimeUtc,
                        SizeBytes = fileInfo.Length,
                        Extension = fileInfo.Extension,
                        Sha256 = duplicateResult.Hash,
                        IsDuplicate = duplicateResult.IsDuplicate,
                        CanonicalSourcePath = duplicateResult.CanonicalPath ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping file '{file}'. Reason: {ex.Message}");
                }
            }

            report.PrintSummary();
            // One CSV row per processed file; this is the seed input for later API/database import.
            var manifestPath = CsvExportService.Export(outputRoot, processedRecords);
            Console.WriteLine($"CSV manifest created: {manifestPath}");
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
    }
}
