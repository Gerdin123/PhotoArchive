using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Reads metadata from files used during organization.</summary>
    internal interface IMetadataExtractor
    {
        /// <summary>Attempts to read EXIF/image metadata from the file.</summary>
        bool TryExtract(string filePath, out ExtractedMetadata metadata);
    }
}
