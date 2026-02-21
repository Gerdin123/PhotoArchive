using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services;

internal sealed class OutputRecordBuilder(
    bool groupThumbnails,
    bool groupLegacyProgramFiles,
    IFileMover mover,
    IReportService report)
{
    private static readonly HashSet<string> LegacyProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".db", ".info", ".dat", ".pe4", ".idx"
    };

    public ProcessedFileRecord Build(AnalyzedFile file, string canonicalPath)
    {
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
            if (groupThumbnails && ThumbnailDetector.IsThumbnailFile(file.SourcePath))
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

        return new ProcessedFileRecord
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
        };
    }
}
