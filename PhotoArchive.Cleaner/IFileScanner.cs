namespace PhotoArchive.Cleaner
{
    internal interface IFileScanner
    {
        string[] ScanRecursively(string rootPath);
    }

    internal class FileScanner : IFileScanner
    {
        public string[] ScanRecursively(string rootPath)
        {
            return Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories);
        }
    }
}
