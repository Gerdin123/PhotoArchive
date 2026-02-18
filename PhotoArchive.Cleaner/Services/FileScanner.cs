namespace PhotoArchive.Cleaner.Services
{
    internal class FileScanner : IFileScanner
    {
        public IEnumerable<string> ScanRecursively(string rootPath)
        {
            // Iterative DFS avoids deep recursion and handles large trees safely.
            var pending = new Stack<string>();
            pending.Push(rootPath);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(current);
                }
                catch
                {
                    // Skip folders we cannot read and continue with the rest.
                    continue;
                }

                foreach (var file in files)
                {
                    yield return file;
                }

                IEnumerable<string> directories;
                try
                {
                    directories = Directory.EnumerateDirectories(current);
                }
                catch
                {
                    // Same behavior for inaccessible child directories.
                    continue;
                }

                foreach (var directory in directories)
                {
                    pending.Push(directory);
                }
            }
        }
    }
}
