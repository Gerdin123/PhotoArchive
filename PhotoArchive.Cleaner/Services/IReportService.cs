namespace PhotoArchive.Cleaner.Services
{
    /// <summary>Collects and prints run summary statistics.</summary>
    internal interface IReportService
    {
        /// <summary>Total processed files.</summary>
        int Total { get; }
        /// <summary>Total files placed in Images.</summary>
        int Images { get; }
        /// <summary>Total files placed in Duplicates.</summary>
        int Duplicates { get; }
        /// <summary>Total files placed in Others.</summary>
        int Others { get; }

        /// <summary>Registers an image result.</summary>
        void RegisterImage(string file);
        /// <summary>Registers a duplicate result.</summary>
        void RegisterDuplicate(string file);
        /// <summary>Registers an other-file result.</summary>
        void RegisterOther(string file);

        /// <summary>Writes summary to console.</summary>
        void PrintSummary();
    }
}
