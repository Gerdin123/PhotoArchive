using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Classifies files into cleaner categories.</summary>
    internal interface IFileClassifier
    {
        /// <summary>Returns file type for a given path.</summary>
        FileType Classify(string filename);
    }
}
