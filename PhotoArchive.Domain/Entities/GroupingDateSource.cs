namespace PhotoArchive.Domain.Entities
{
    /// <summary>
    /// Source used when deciding year-based grouping.
    /// </summary>
    public enum GroupingDateSource
    {
        /// <summary>Year came from EXIF DateTaken.</summary>
        DateTaken,
        /// <summary>Year came from a recognized folder date prefix.</summary>
        FolderNamePrefix,
        /// <summary>Year came from file system creation time.</summary>
        FileCreationTime
    }
}
