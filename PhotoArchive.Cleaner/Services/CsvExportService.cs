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
            writer.WriteLine("SourcePath,OutputPath,Bucket,GroupingYear,GroupingDateSource,GroupingDate,DateTaken,CreatedYear,CreatedAtUtc,LastWriteAtUtc,SizeBytes,Extension,Sha256,IsDuplicate,CanonicalSourcePath");

            foreach (var row in records)
            {
                writer.WriteLine(string.Join(",",
                    Escape(row.SourcePath),
                    Escape(row.OutputPath),
                    Escape(row.Bucket),
                    row.GroupingYear.ToString(CultureInfo.InvariantCulture),
                    Escape(row.GroupingDateSource),
                    Escape(row.GroupingDate.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(row.DateTaken?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                    row.CreatedYear.ToString(CultureInfo.InvariantCulture),
                    Escape(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    Escape(row.LastWriteAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                    row.SizeBytes.ToString(CultureInfo.InvariantCulture),
                    Escape(row.Extension),
                    Escape(row.Sha256),
                    row.IsDuplicate ? "true" : "false",
                    Escape(row.CanonicalSourcePath)));
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
