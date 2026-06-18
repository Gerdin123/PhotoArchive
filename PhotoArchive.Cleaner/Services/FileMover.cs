namespace PhotoArchive.Cleaner.Services
{
    internal class FileMover : IFileMover
    {
        private readonly string outputPath;

        public FileMover(string rootPath, string outputPath)
        {
            this.outputPath = Path.GetFullPath(outputPath);

            // Pre-create top-level buckets for predictable output structure.
            Directory.CreateDirectory(Path.Combine(this.outputPath, "Images"));
            Directory.CreateDirectory(Path.Combine(this.outputPath, "Duplicates"));
            Directory.CreateDirectory(Path.Combine(this.outputPath, "Others"));
        }

        public string MoveToDuplicates(string file) => Move(file, "Duplicates", null);

        public string MoveToImages(string file, int year, DateTime groupingDate, int dayIndex)
        {
            var extension = Path.GetExtension(file);
            var fileName = $"{groupingDate:yyyyMMdd} - {dayIndex:D2}{extension}";
            return Move(file, "Images", year, month: null, fileNameOverride: fileName);
        }

        public string MoveToOthers(string file, int year, int month) => Move(file, "Others", year, month);

        public string MoveToOthersCategory(string file, string category)
            => Move(file, Path.Combine("Others", category), null);

        private string BuildPath(string destination, string file, int? year, int? month, string? fileNameOverride)
        {
            var fileName = string.IsNullOrWhiteSpace(fileNameOverride) ? Path.GetFileName(file) : fileNameOverride;
            var targetPath = year.HasValue && month.HasValue
                ? Path.Combine(outputPath, destination, year.Value.ToString("0000"), month.Value.ToString("00"), fileName)
                : year.HasValue
                    ? Path.Combine(outputPath, destination, year.Value.ToString("0000"), fileName)
                : Path.Combine(outputPath, destination, fileName);

            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            if (!File.Exists(targetPath))
                return targetPath;

            // If a name collision occurs, append _1, _2, ... to keep all files.
            var extension = Path.GetExtension(targetPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
            var directory = Path.GetDirectoryName(targetPath)!;

            var i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_{i}{extension}");
                i++;
            } while (File.Exists(candidate));

            return candidate;
        }

        private string Move(string file, string destination, int? year, int? month = null, string? fileNameOverride = null)
        {
            var destinationPath = BuildPath(destination, file, year, month, fileNameOverride);
            File.Copy(file, destinationPath, overwrite: false);
            return destinationPath;
        }
    }
}
