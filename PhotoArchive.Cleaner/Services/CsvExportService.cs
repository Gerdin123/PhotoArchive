using System.Globalization;
using System.Text;
using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    internal sealed class CsvExportService
    {
        public static string Export(string outputRoot, IEnumerable<ProcessedFileRecord> records)
        {
            var csvPath = Path.Combine(outputRoot, "cleaned_manifest.csv");
            using var writer = new StreamWriter(csvPath, append: false, Encoding.UTF8);

            // Header uses stable names so importer code can rely on exact column keys.
            writer.WriteLine("ImportBatchId,SourcePath,OutputPath,Bucket,Sha256,SizeBytes,Extension,Width,Height,Orientation,CameraMake,CameraModel,ExifTags,ExifDateTimeOriginal,ExifCreateDate,ExifModifyDate,FolderDateCandidate,CreatedAtUtc,LastWriteAtUtc,CleanerBestDate,CleanerBestDateSource,GroupingYear,GroupingDate,IsDuplicate,CanonicalSourcePath,PerceptualHash");

            foreach (var row in records)
            {
                writer.WriteLine(string.Join(",",
                    Escape(row.ImportBatchId),
                    Escape(row.SourcePath),
                    Escape(row.OutputPath),
                    Escape(row.Bucket),
                    Escape(row.Sha256),
                    row.SizeBytes.ToString(CultureInfo.InvariantCulture),
                    Escape(row.Extension),
                    Escape(row.Width?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.Height?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.Orientation?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.CameraMake),
                    Escape(row.CameraModel),
                    Escape(row.ExifTags),
                    Escape(row.ExifDateTimeOriginal?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.ExifCreateDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.ExifModifyDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.FolderDateCandidate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    Escape(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(row.LastWriteAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(row.CleanerBestDate.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(row.CleanerBestDateSource),
                    row.GroupingYear.ToString(CultureInfo.InvariantCulture),
                    Escape(row.GroupingDate.ToString("O", CultureInfo.InvariantCulture)),
                    row.IsDuplicate ? "true" : "false",
                    Escape(row.CanonicalSourcePath),
                    Escape(row.PerceptualHash)));
            }

            return csvPath;
        }

        private static string Escape(string value)
        {
            // RFC-4180 style escaping for commas, quotes, and line breaks.
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
