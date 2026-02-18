namespace PhotoArchive.Cleaner.Models
{
    /// <summary>One row written to <c>cleaned_manifest.csv</c>.</summary>
    internal sealed class ProcessedFileRecord
    {
        /// <summary>Full source path from input tree.</summary>
        public string SourcePath { get; init; } = string.Empty;
        /// <summary>Full destination path in cleaned tree.</summary>
        public string OutputPath { get; init; } = string.Empty;
        /// <summary>Bucket value: Images, Duplicates, or Others.</summary>
        public string Bucket { get; init; } = string.Empty;
        /// <summary>Year chosen for folder grouping.</summary>
        public int GroupingYear { get; init; }
        /// <summary>Source of grouping date (DateTaken or FileCreationTime).</summary>
        public string GroupingDateSource { get; init; } = string.Empty;
        /// <summary>Exact date used to derive <see cref="GroupingYear"/>.</summary>
        public DateTime GroupingDate { get; init; }
        /// <summary>EXIF DateTaken when available.</summary>
        public DateTime? DateTaken { get; init; }
        /// <summary>Creation year from file system.</summary>
        public int CreatedYear { get; init; }
        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedAtUtc { get; init; }
        /// <summary>Last write timestamp in UTC.</summary>
        public DateTime LastWriteAtUtc { get; init; }
        /// <summary>File size in bytes.</summary>
        public long SizeBytes { get; init; }
        /// <summary>File extension (for example .jpg).</summary>
        public string Extension { get; init; } = string.Empty;
        /// <summary>SHA-256 hash in hex format.</summary>
        public string Sha256 { get; init; } = string.Empty;
        /// <summary>True when file duplicates a previously seen file.</summary>
        public bool IsDuplicate { get; init; }
        /// <summary>Source path of the first file with matching hash.</summary>
        public string CanonicalSourcePath { get; init; } = string.Empty;
    }
}
