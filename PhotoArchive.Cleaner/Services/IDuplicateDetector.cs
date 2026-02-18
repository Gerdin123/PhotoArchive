using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Detects duplicate files using content hash.</summary>
    internal interface IDuplicateDetector
    {
        /// <summary>Computes SHA-256 hash for duplicate grouping.</summary>
        string ComputeHash(string filePath);
        /// <summary>Registers a file and returns duplicate result details.</summary>
        DuplicateCheckResult Register(string filePath);
    }
}
