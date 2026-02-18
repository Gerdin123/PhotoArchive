namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Enumerates all files under a root folder.</summary>
    internal interface IFileScanner
    {
        /// <summary>Returns file paths recursively below <paramref name="rootPath"/>.</summary>
        IEnumerable<string> ScanRecursively(string rootPath);
    }
}
