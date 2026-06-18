using PhotoArchive.Cleaner.Models;
using PhotoArchive.Cleaner.Services;
using System.Diagnostics;

namespace PhotoArchive.Cleaner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (!CleanerOptionsResolver.TryResolve(args, out var options))
            {
                return;
            }

            Console.WriteLine($"Source: {options.SourcePath}");
            Console.WriteLine($"Output: {options.OutputRoot}");
            Console.WriteLine("Date source preference: DateTimeOriginal -> CreateDate -> FolderStructure -> CreationTime -> LastWriteTime");
            Console.WriteLine("Image filtering: known image extensions are kept in Images; thumbnail-like names are routed to Others.");
            Console.WriteLine($"Others/Thumbnails enabled: {options.GroupThumbnails}");
            Console.WriteLine($"Others/OldProgramSpecific enabled: {options.GroupLegacyProgramFiles}");

            IFileScanner scanner = new FileScanner();
            IFileClassifier classifier = new FileClassifier();
            IMetadataExtractor metadataExtractor = new ExifMetadataExtractor();
            IDuplicateDetector duplicateDetector = new DuplicateDetector();
            IFileMover mover = new FileMover(options.SourcePath, options.OutputRoot);
            IReportService report = new ReportService();

            var fileAnalyzer = new FileAnalyzer(
                options.SourcePath,
                classifier,
                metadataExtractor,
                duplicateDetector);
            var recordBuilder = new OutputRecordBuilder(
                options.GroupThumbnails,
                options.GroupLegacyProgramFiles,
                mover,
                report);
            var importBatchId = Guid.NewGuid().ToString("N");

            var processedRecords = new List<ProcessedFileRecord>();
            var totalStopwatch = Stopwatch.StartNew();
            var files = scanner.ScanRecursively(options.SourcePath).ToList();
            var totalFiles = files.Count;
            Console.WriteLine($"Files discovered: {totalFiles}");
            if (totalFiles == 0)
            {
                totalStopwatch.Stop();
                report.PrintSummary();
                var emptyManifestPath = CsvExportService.Export(options.OutputRoot, processedRecords);
                Console.WriteLine($"CSV manifest created: {emptyManifestPath}");
                Console.WriteLine($"Total time: {ConsoleProgressRenderer.FormatDuration(totalStopwatch.Elapsed)}");
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
                    analyzedFiles.Add(fileAnalyzer.Analyze(file));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping file '{file}'. Reason: {ex.Message}");
                }

                progressCurrent++;
                ConsoleProgressRenderer.RenderProgress(progressCurrent, progressTotal, totalStopwatch.Elapsed, "Analyzing");
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
            var dayIndexBySourcePath = BuildDayIndexBySourcePath(analyzedFiles, canonicalByHash);

            foreach (var file in analyzedFiles)
            {
                var canonicalPath = canonicalByHash[file.Sha256];
                var dayIndex = dayIndexBySourcePath.TryGetValue(file.SourcePath, out var index) ? index : 0;
                var record = recordBuilder.Build(file, canonicalPath, importBatchId, dayIndex);
                processedRecords.Add(record);

                progressCurrent++;
                ConsoleProgressRenderer.RenderProgress(progressCurrent, progressTotal, totalStopwatch.Elapsed, "Copying");
            }

            totalStopwatch.Stop();
            Console.WriteLine();
            report.PrintSummary();
            var manifestPath = CsvExportService.Export(options.OutputRoot, processedRecords);
            Console.WriteLine($"CSV manifest created: {manifestPath}");
            Console.WriteLine($"Total time: {ConsoleProgressRenderer.FormatDuration(totalStopwatch.Elapsed)}");
            Console.WriteLine("Metadata is preserved because files are copied byte-for-byte without modification.");
        }

        private static IReadOnlyDictionary<string, int> BuildDayIndexBySourcePath(
            IReadOnlyList<AnalyzedFile> analyzedFiles,
            IReadOnlyDictionary<string, string> canonicalByHash)
        {
            var dayIndexBySourcePath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var canonicalImages = analyzedFiles
                .Where(x => x.FileType == FileType.Image)
                .Where(x => canonicalByHash.TryGetValue(x.Sha256, out var canonicalPath)
                    && canonicalPath.Equals(x.SourcePath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.GroupingDate)
                .ThenBy(x => x.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var dayGroup in canonicalImages.GroupBy(x => x.GroupingDate.Date))
            {
                var index = 1;
                foreach (var file in dayGroup)
                {
                    dayIndexBySourcePath[file.SourcePath] = index;
                    index++;
                }
            }

            return dayIndexBySourcePath;
        }
    }
}
