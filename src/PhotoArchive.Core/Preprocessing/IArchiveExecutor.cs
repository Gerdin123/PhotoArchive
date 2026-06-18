namespace PhotoArchive.Core.Preprocessing;

public interface IArchiveExecutor
{
    Task<IReadOnlyList<PlannedFileOperation>> ExecuteAsync(OutputPlan plan, CancellationToken cancellationToken = default);
}
