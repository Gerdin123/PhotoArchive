namespace PhotoArchive.Domain.Entities
{
    /// <summary>
    /// Source used when deciding year-based grouping.
    /// </summary>
    public enum GroupingDateSource
    {
        /// <summary>Date came from EXIF DateTimeOriginal.</summary>
        DateTimeOriginal,
        /// <summary>Date came from EXIF CreateDate (DateTimeDigitized).</summary>
        CreateDate,
        /// <summary>Date came from a recognized folder date prefix.</summary>
        FolderStructure,
        /// <summary>Year came from file system creation time.</summary>
        FileCreationTime,
        /// <summary>Date came from file system last write time.</summary>
        LastWriteTime,
        /// <summary>Legacy name for DateTimeOriginal.</summary>
        DateTaken,
        /// <summary>Legacy name for FolderStructure.</summary>
        FolderNamePrefix
    }
}
