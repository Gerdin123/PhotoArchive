using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure.Manifest;

public sealed class OperationLogWriter
{
    public async Task<string> WriteAsync(OutputPlan plan, CancellationToken cancellationToken = default)
    {
        var manifestDirectory = Path.Combine(plan.Settings.OutputRoot, "Manifests");
        Directory.CreateDirectory(manifestDirectory);

        var fileName = $"operations-{plan.RunStartedAtUtc:yyyyMMdd-HHmmss}.log";
        var path = Path.Combine(manifestDirectory, fileName);

        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream);

        foreach (var operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(
                $"{operation.ExecutionResult}\t{operation.MediaKind}\t{operation.SourcePath}\t{operation.DestinationPath}\t{operation.ErrorMessage ?? string.Empty}");
        }

        return path;
    }
}
