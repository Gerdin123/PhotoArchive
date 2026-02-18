namespace PhotoArchive.Cleaner.Models
{
    /// <summary>Result from duplicate registration for one file.</summary>
    internal sealed class DuplicateCheckResult
    {
        /// <summary>True when hash already exists in the current run.</summary>
        public bool IsDuplicate { get; init; }
        /// <summary>First source path with this hash.</summary>
        public string? CanonicalPath { get; init; }
        /// <summary>SHA-256 content hash in hex format.</summary>
        public string Hash { get; init; } = string.Empty;
    }
}
