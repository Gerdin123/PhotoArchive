namespace PhotoArchive.Domain.Entities
{
    /// <summary>
    /// Represents one processed file from the cleaner manifest.
    /// </summary>
    public class Photo
    {
        /// <summary>Primary key.</summary>
        public int Id { get; set; }

        /// <summary>Original path in the source folder tree.</summary>
        public required string SourcePath { get; set; }
        /// <summary>Copied path in the cleaned output tree.</summary>
        public required string OutputPath { get; set; }

        /// <summary>File name including extension.</summary>
        public required string FileName { get; set; }
        /// <summary>File extension (for example .jpg).</summary>
        public required string Extension { get; set; }
        /// <summary>Source file size in bytes.</summary>
        public long SizeBytes { get; set; }

        /// <summary>SHA-256 hash of file content.</summary>
        public required string Sha256 { get; set; }

        /// <summary>Cleaner bucket classification.</summary>
        public PhotoBucket Bucket { get; set; }
        /// <summary>True when this file is a duplicate of a previously seen file.</summary>
        public bool IsDuplicate { get; set; }
        /// <summary>Source path of the first file with the same hash.</summary>
        public string? CanonicalSourcePath { get; set; }

        /// <summary>Year used for folder grouping in cleaned output.</summary>
        public int GroupingYear { get; set; }
        /// <summary>Indicates whether grouping used DateTaken or file creation time.</summary>
        public GroupingDateSource GroupingDateSource { get; set; }
        /// <summary>Exact date value used for grouping.</summary>
        public DateTime GroupingDate { get; set; }
        /// <summary>EXIF date taken when available.</summary>
        public DateTime? DateTaken { get; set; }
        /// <summary>Source file creation time in UTC.</summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>Source file last write time in UTC.</summary>
        public DateTime LastWriteAtUtc { get; set; }

        /// <summary>Optional image width in pixels.</summary>
        public int? Width { get; set; }
        /// <summary>Optional image height in pixels.</summary>
        public int? Height { get; set; }

        /// <summary>Many-to-many links between photo and tags.</summary>
        public ICollection<PhotoTag> PhotoTags { get; set; } = [];
        /// <summary>Many-to-many links between photo and people.</summary>
        public ICollection<PhotoPerson> PhotoPeople { get; set; } = [];
    }
}
