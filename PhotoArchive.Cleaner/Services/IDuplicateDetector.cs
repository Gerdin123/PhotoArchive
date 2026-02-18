using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Detects duplicate files using content hash.</summary>
    internal interface IDuplicateDetector
    {
        /// <summary>Registers a file and returns duplicate result details.</summary>
        DuplicateCheckResult Register(string filePath);
    }
}
