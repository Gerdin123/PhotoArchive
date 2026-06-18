using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class ArchiveExecutor : IArchiveExecutor
{
    private readonly IHashService hashService;

    public ArchiveExecutor(IHashService hashService)
    {
        this.hashService = hashService;
    }

    public async Task<IReadOnlyList<PlannedFileOperation>> ExecuteAsync(
        OutputPlan plan,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PlannedFileOperation>(plan.Operations.Count);

        foreach (var operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(operation.DestinationPath))
                {
                    results.Add(operation with
                    {
                        ExecutionResult = "Failed",
                        ErrorMessage = "Destination already exists."
                    });
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(operation.DestinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(operation.SourcePath, operation.DestinationPath, overwrite: false);
                var copiedHash = await hashService.ComputeSha256Async(operation.DestinationPath, cancellationToken);
                if (!copiedHash.Equals(operation.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(operation with
                    {
                        ExecutionResult = "Failed",
                        ErrorMessage = "Copied file hash did not match source hash."
                    });
                    continue;
                }

                results.Add(operation with { ExecutionResult = "Copied" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                results.Add(operation with
                {
                    ExecutionResult = "Failed",
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }
}
