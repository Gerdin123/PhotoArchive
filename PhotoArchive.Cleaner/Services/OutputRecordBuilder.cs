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

    public ProcessedFileRecord Build(AnalyzedFile file, string canonicalPath, string importBatchId, int dayIndex)
    {
        var isDuplicate = !string.Equals(file.SourcePath, canonicalPath, StringComparison.OrdinalIgnoreCase);
        var groupingYear = file.GroupingDate.Year;

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
            outputPath = mover.MoveToImages(file.SourcePath, groupingYear, file.GroupingDate, dayIndex);
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
                outputPath = mover.MoveToOthers(file.SourcePath, groupingYear, file.GroupingDate.Month);
            }

            report.RegisterOther(file.SourcePath);
        }

        return new ProcessedFileRecord
        {
            ImportBatchId = importBatchId,
            SourcePath = file.SourcePath,
            OutputPath = outputPath,
            Bucket = bucket,
            Sha256 = file.Sha256,
            SizeBytes = file.SizeBytes,
            Extension = file.Extension,
            Width = file.Width,
            Height = file.Height,
            Orientation = file.Orientation,
            CameraMake = file.CameraMake,
            CameraModel = file.CameraModel,
            ExifTags = string.Join(";", file.ExifTags),
            ExifDateTimeOriginal = file.ExifDateTimeOriginal,
            ExifCreateDate = file.ExifCreateDate,
            ExifModifyDate = file.ExifModifyDate,
            FolderDateCandidate = file.FolderDateCandidate,
            CreatedAtUtc = file.CreatedAtUtc,
            LastWriteAtUtc = file.LastWriteAtUtc,
            CleanerBestDate = file.GroupingDate,
            CleanerBestDateSource = file.GroupingDateSource,
            GroupingYear = groupingYear,
            GroupingDate = file.GroupingDate,
            IsDuplicate = isDuplicate,
            CanonicalSourcePath = isDuplicate ? canonicalPath : string.Empty,
            PerceptualHash = file.PerceptualHash
        };
    }
}
