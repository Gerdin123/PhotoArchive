namespace PhotoArchive.Cleaner.Models
{
    /// <summary>Minimal file categories used by the cleaner.</summary>
    internal enum FileType
    {
        /// <summary>Supported image file.</summary>
        Image,
        /// <summary>Non-image or filtered image (for example thumbnails).</summary>
        Other
    }
}
