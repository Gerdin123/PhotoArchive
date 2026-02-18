using System.Security.Cryptography;
using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    internal class DuplicateDetector : IDuplicateDetector
    {
        // Maps file hash -> first path seen. Later matches become duplicates.
        private readonly Dictionary<string, string> firstPathByHash = new(StringComparer.OrdinalIgnoreCase);

        public string ComputeHash(string filePath) => ComputeSha256(filePath);

        public DuplicateCheckResult Register(string filePath)
        {
            var hash = ComputeHash(filePath);
            if (firstPathByHash.TryGetValue(hash, out var firstPath))
            {
                return new DuplicateCheckResult
                {
                    IsDuplicate = true,
                    CanonicalPath = firstPath,
                    Hash = hash
                };
            }

            firstPathByHash[hash] = filePath;
            return new DuplicateCheckResult
            {
                IsDuplicate = false,
                Hash = hash
            };
        }

        private static string ComputeSha256(string filePath)
        {
            // Hashing full file content is slower than file-size checks,
            // but gives reliable duplicate detection.
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }
    }
}
