using System.Text;

namespace PhotoArchive.Cleaner.Services
{
    internal sealed class ReportService : IReportService
    {
        // Lists are only used for counts right now; they can later be used for detailed logs.
        private readonly List<string> images = [];
        private readonly List<string> duplicates = [];
        private readonly List<string> others = [];

        public int Total => Images + Duplicates + Others;
        public int Images => images.Count;
        public int Duplicates => duplicates.Count;
        public int Others => others.Count;

        public void RegisterImage(string file) => images.Add(file);

        public void RegisterDuplicate(string file) => duplicates.Add(file);

        public void RegisterOther(string file) => others.Add(file);

        public void PrintSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("Cleaning complete:");
            sb.AppendLine($"  Total files processed: {Total}");
            sb.AppendLine($"  Images: {Images}");
            sb.AppendLine($"  Duplicates: {Duplicates}");
            sb.AppendLine($"  Others: {Others}");
            Console.WriteLine(sb.ToString());
        }
    }
}
