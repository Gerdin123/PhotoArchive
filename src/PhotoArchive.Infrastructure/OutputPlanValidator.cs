using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class OutputPlanValidator
{
    public IReadOnlyList<string> Validate(OutputPlan plan)
    {
        var errors = new List<string>();

        if (!plan.Settings.AllowOutputInsideInput && IsSubPathOf(plan.Settings.OutputRoot, plan.Settings.InputRoot))
        {
            errors.Add("Output path cannot be inside input path unless --allow-output-inside-input is specified.");
        }

        var existingDestination = plan.Operations.FirstOrDefault(operation => File.Exists(operation.DestinationPath));
        if (existingDestination is not null)
        {
            errors.Add($"Destination already exists: {existingDestination.DestinationPath}");
        }

        var requiredBytes = plan.Operations.Sum(operation => new FileInfo(operation.SourcePath).Length);
        var driveRoot = Path.GetPathRoot(Path.GetFullPath(plan.Settings.OutputRoot));
        if (!string.IsNullOrWhiteSpace(driveRoot))
        {
            var drive = new DriveInfo(driveRoot);
            if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
            {
                errors.Add("Output drive does not have enough free space for copy execution.");
            }
        }

        return errors;
    }

    private static bool IsSubPathOf(string candidatePath, string parentPath)
    {
        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        var parent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
            && !candidate.Equals(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
