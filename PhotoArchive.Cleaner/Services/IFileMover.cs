namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Copies files into cleaned output folders.</summary>
    internal interface IFileMover
    {
        /// <summary>Copies an image file to Images/&lt;year&gt; and returns destination path.</summary>
        string MoveToImages(string file, int year);
        /// <summary>Copies a duplicate file to Duplicates and returns destination path.</summary>
        string MoveToDuplicates(string file);
        /// <summary>Copies a non-image file to Others/&lt;year&gt; and returns destination path.</summary>
        string MoveToOthers(string file, int year);
    }
}
