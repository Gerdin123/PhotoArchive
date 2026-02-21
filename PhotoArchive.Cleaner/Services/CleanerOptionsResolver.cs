using PhotoArchive.Cleaner.Models;

namespace PhotoArchive.Cleaner.Services;

internal static class CleanerOptionsResolver
{
    public static bool TryResolve(string[] args, out CleanerOptions options)
    {
        options = null!;

        var sourcePath = ResolveSourcePath(args);
        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            Console.WriteLine("The provided folder does not exist.");
            return false;
        }

        var outputRoot = CreateOutputFolder(sourcePath);
        var useFolderDate = ResolveBooleanPreference(
            args,
            "--date-from-folder",
            "Is Date based on folder structure? [Y/n]: ",
            defaultValue: true);
        var groupThumbnails = ResolveBooleanPreference(
            args,
            "--others-thumbnails-folder",
            "Put thumbnail files in Others/Thumbnails? [Y/n]: ",
            defaultValue: true);
        var groupLegacyProgramFiles = ResolveBooleanPreference(
            args,
            "--others-legacy-folder",
            "Put .db/.info/.dat/.pe4/.idx files in Others/OldProgramSpecific? [Y/n]: ",
            defaultValue: true);

        options = new CleanerOptions(
            SourcePath: sourcePath,
            OutputRoot: outputRoot,
            UseFolderDate: useFolderDate,
            GroupThumbnails: groupThumbnails,
            GroupLegacyProgramFiles: groupLegacyProgramFiles);
        return true;
    }

    private static string ResolveSourcePath(string[] args)
    {
        if (args.Length > 0)
        {
            return Path.GetFullPath(args[0]);
        }

        Console.Write("Enter folder path to clean: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input.Trim());
    }

    private static bool ResolveBooleanPreference(string[] args, string optionName, string prompt, bool defaultValue)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length && TryParseBooleanOption(args[i + 1], out var nextValue))
                {
                    return nextValue;
                }

                return true;
            }

            if (arg.StartsWith($"{optionName}=", StringComparison.OrdinalIgnoreCase))
            {
                var valueText = arg[(optionName.Length + 1)..];
                if (TryParseBooleanOption(valueText, out var inlineValue))
                {
                    return inlineValue;
                }
            }
        }

        Console.Write(prompt);
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return TryParseBooleanOption(input, out var parsed) ? parsed : defaultValue;
    }

    internal static bool TryParseBooleanOption(string value, out bool parsed)
    {
        var normalized = value.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }

        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("n", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            parsed = false;
            return true;
        }

        parsed = false;
        return false;
    }

    internal static string CreateOutputFolder(string sourcePath)
    {
        var sourceDirectory = new DirectoryInfo(sourcePath);
        var parentDirectory = sourceDirectory.Parent?.FullName ?? sourceDirectory.FullName;
        var baseName = string.IsNullOrWhiteSpace(sourceDirectory.Name) ? "root" : sourceDirectory.Name;

        var outputPath = Path.Combine(parentDirectory, $"{baseName}_cleaned");
        if (Directory.Exists(outputPath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputPath = Path.Combine(parentDirectory, $"{baseName}_cleaned_{timestamp}");
        }

        Directory.CreateDirectory(outputPath);
        return outputPath;
    }
}
