namespace PhotoArchive.Cleaner.Models
{
    /// <summary>One row written to <c>cleaned_manifest.csv</c>.</summary>
    internal sealed class ProcessedFileRecord
    {
        /// <summary>Unique identifier for the cleaner run.</summary>
        public string ImportBatchId { get; init; } = string.Empty;
        /// <summary>Full source path from input tree.</summary>
        public string SourcePath { get; init; } = string.Empty;
        /// <summary>Full destination path in cleaned tree.</summary>
        public string OutputPath { get; init; } = string.Empty;
        /// <summary>Bucket value: Images, Duplicates, or Others.</summary>
        public string Bucket { get; init; } = string.Empty;
        /// <summary>SHA-256 hash in hex format.</summary>
        public string Sha256 { get; init; } = string.Empty;
        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; init; }
        /// <summary>File extension (for example .jpg).</summary>
        public string Extension { get; init; } = string.Empty;
        /// <summary>Image width in pixels when known.</summary>
        public int? Width { get; init; }
        /// <summary>Image height in pixels when known.</summary>
        public int? Height { get; init; }
        /// <summary>EXIF orientation value when known.</summary>
        public int? Orientation { get; init; }
        /// <summary>Camera make from EXIF when known.</summary>
        public string CameraMake { get; init; } = string.Empty;
        /// <summary>Camera model from EXIF when known.</summary>
        public string CameraModel { get; init; } = string.Empty;
        /// <summary>EXIF keyword/tag values extracted from the file.</summary>
        public string ExifTags { get; init; } = string.Empty;
        /// <summary>EXIF DateTimeOriginal when available.</summary>
        public DateTime? ExifDateTimeOriginal { get; init; }
        /// <summary>EXIF CreateDate when available.</summary>
        public DateTime? ExifCreateDate { get; init; }
        /// <summary>EXIF ModifyDate when available.</summary>
        public DateTime? ExifModifyDate { get; init; }
        /// <summary>Date parsed from folder structure when available.</summary>
        public DateTime? FolderDateCandidate { get; init; }
        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedAtUtc { get; init; }
        /// <summary>Last write timestamp in UTC.</summary>
        public DateTime LastWriteAtUtc { get; init; }
        /// <summary>Date selected by cleaner ranking logic.</summary>
        public DateTime CleanerBestDate { get; init; }
        /// <summary>Selected source for <see cref="CleanerBestDate"/>.</summary>
        public string CleanerBestDateSource { get; init; } = string.Empty;
        /// <summary>Year chosen for folder grouping.</summary>
        public int GroupingYear { get; init; }
        /// <summary>Exact date used to derive <see cref="GroupingYear"/>.</summary>
        public DateTime GroupingDate { get; init; }
        /// <summary>True when file duplicates a previously seen file.</summary>
        public bool IsDuplicate { get; init; }
        /// <summary>Source path of the first file with matching hash.</summary>
        public string CanonicalSourcePath { get; init; } = string.Empty;
        /// <summary>Perceptual hash when available.</summary>
        public string PerceptualHash { get; init; } = string.Empty;
    }
}
