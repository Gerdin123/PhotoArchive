namespace PhotoArchive.Cleaner
{
    internal interface IReportService
    {
        int Total { get; }
        int Images { get; }
        int Duplicates { get; }
        int Others { get; }

        void RegisterImage(string file);
        void RegisterDuplicate(string file);
        void RegisterOther(string file);

        void PrintSummary();
    }
}
