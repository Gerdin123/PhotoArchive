using System.Globalization;

namespace PhotoArchive.Cleaner.Services;

internal static class ConsoleProgressRenderer
{
    public static void RenderProgress(int processed, int total, TimeSpan elapsed, string activity)
    {
        const int barWidth = 30;
        var ratio = total == 0 ? 1d : (double)processed / total;
        var completed = (int)Math.Round(ratio * barWidth, MidpointRounding.AwayFromZero);
        completed = Math.Clamp(completed, 0, barWidth);
        var bar = new string('#', completed) + new string('-', barWidth - completed);
        var percent = ratio * 100;
        var eta = EstimateEta(processed, total, elapsed);
        var etaText = eta.HasValue ? FormatDuration(eta.Value) : "--:--";

        Console.Write($"\r[{bar}] {percent,6:0.00}%  {processed}/{total}  {activity,-9} Elapsed {FormatDuration(elapsed)}  ETA {etaText}");
    }

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static TimeSpan? EstimateEta(int processed, int total, TimeSpan elapsed)
    {
        if (processed <= 0 || total <= 0 || processed >= total || elapsed.TotalMilliseconds <= 0)
        {
            return null;
        }

        var avgMsPerFile = elapsed.TotalMilliseconds / processed;
        var remaining = total - processed;
        return TimeSpan.FromMilliseconds(avgMsPerFile * remaining);
    }
}
