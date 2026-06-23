namespace PhotoArchive.App.Review;

public sealed record DirectorySetupProgress(
    string Phase,
    string Message,
    int FilesFound,
    int FilesProcessed,
    int? TotalFiles,
    double Percentage,
    TimeSpan Elapsed,
    TimeSpan? EstimatedRemaining)
{
    public static DirectorySetupProgress Create(
        string phase,
        string message,
        int filesFound,
        int filesProcessed,
        int? totalFiles,
        DateTimeOffset startedAtUtc)
    {
        var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
        var percentage = totalFiles is > 0
            ? Math.Clamp(filesProcessed * 100d / totalFiles.Value, 0d, 100d)
            : 0d;
        TimeSpan? eta = filesProcessed > 0 && totalFiles is > 0 && filesProcessed < totalFiles.Value
            ? TimeSpan.FromTicks((long)(elapsed.Ticks / (double)filesProcessed * (totalFiles.Value - filesProcessed)))
            : null;

        return new DirectorySetupProgress(
            phase,
            message,
            filesFound,
            filesProcessed,
            totalFiles,
            percentage,
            elapsed,
            eta);
    }
}
