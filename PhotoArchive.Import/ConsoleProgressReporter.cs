namespace PhotoArchive.Import;

internal sealed class ConsoleProgressReporter : IDisposable
{
    private readonly int totalRows;
    private readonly DateTime startedAtUtc;
    private int lastRenderedLength;
    private int lastProcessed;
    private DateTime lastRenderAtUtc;

    public ConsoleProgressReporter(int totalRows)
    {
        this.totalRows = totalRows;
        startedAtUtc = DateTime.UtcNow;
        lastRenderAtUtc = DateTime.MinValue;
        Console.WriteLine(totalRows > 0
            ? $"Importing {totalRows:N0} rows from manifest..."
            : "Importing rows from manifest...");
    }

    public void WriteMessage(string message)
    {
        ClearCurrentLine();
        Console.WriteLine(message);
    }

    public void Report(
        int processed,
        int imported,
        int skippedFiltered,
        int skippedDuplicateHash,
        int skippedInvalid,
        bool force = false)
    {
        var nowUtc = DateTime.UtcNow;
        if (!force)
        {
            var dueToTime = (nowUtc - lastRenderAtUtc).TotalMilliseconds >= 250;
            var dueToCount = processed - lastProcessed >= 100;
            if (!dueToTime && !dueToCount)
            {
                return;
            }
        }

        lastProcessed = processed;
        lastRenderAtUtc = nowUtc;

        var skippedTotal = skippedFiltered + skippedDuplicateHash + skippedInvalid;
        var elapsedSeconds = Math.Max((nowUtc - startedAtUtc).TotalSeconds, 0.001d);
        var rowsPerSecond = processed / elapsedSeconds;
        var progressText = totalRows > 0
            ? $"{processed:N0}/{totalRows:N0} ({Math.Min(100d, processed * 100d / totalRows):0.0}%)"
            : $"{processed:N0}";

        var bar = BuildBar(processed, totalRows, 24);
        var line = $"\r[{bar}] {progressText} | imported {imported:N0} | skipped {skippedTotal:N0} | {rowsPerSecond:0.0} rows/s";
        if (line.Length < lastRenderedLength)
        {
            line += new string(' ', lastRenderedLength - line.Length);
        }

        lastRenderedLength = line.Length;
        Console.Write(line);
    }

    public void Complete(
        int processed,
        int imported,
        int skippedFiltered,
        int skippedDuplicateHash,
        int skippedInvalid)
    {
        Report(processed, imported, skippedFiltered, skippedDuplicateHash, skippedInvalid, force: true);
        Console.WriteLine();
        lastRenderedLength = 0;
    }

    public void Dispose()
    {
        ClearCurrentLine();
    }

    private void ClearCurrentLine()
    {
        if (lastRenderedLength <= 0)
        {
            return;
        }

        Console.Write("\r");
        Console.Write(new string(' ', lastRenderedLength));
        Console.Write("\r");
        lastRenderedLength = 0;
    }

    private static string BuildBar(int processed, int total, int width)
    {
        if (total <= 0)
        {
            return new string('.', width);
        }

        var ratio = Math.Clamp(processed / (double)total, 0d, 1d);
        var filled = (int)Math.Round(ratio * width, MidpointRounding.AwayFromZero);
        if (filled > width)
        {
            filled = width;
        }

        return new string('#', filled) + new string('-', width - filled);
    }
}
