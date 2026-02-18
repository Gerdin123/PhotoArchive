namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Reads metadata from files used during organization.</summary>
    internal interface IMetadataExtractor
    {
        /// <summary>Attempts to read EXIF DateTaken from the file.</summary>
        bool TryGetDateTaken(string filePath, out DateTime dateTaken);
    }
}
