namespace PhotoArchive.Import;

internal static class ImportOptionsResolver
{
    private const string ManifestFileName = "cleaned_manifest.csv";
    private const string DefaultDatabaseFileName = "photoarchive.db";

    public static bool TryResolve(string[] args, out ImportOptions options)
    {
        options = null!;

        var cleanedFolder = ResolveCleanedFolder(args);
        if (string.IsNullOrWhiteSpace(cleanedFolder) || !Directory.Exists(cleanedFolder))
        {
            Console.WriteLine("The provided cleaned folder does not exist.");
            return false;
        }

        var manifestPath = Path.Combine(cleanedFolder, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"Could not find '{ManifestFileName}' in '{cleanedFolder}'.");
            return false;
        }

        var databasePath = ResolveDatabasePath(args, cleanedFolder);
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            Console.WriteLine("No database path was provided.");
            return false;
        }

        if (!PrepareDatabaseLocation(args, databasePath))
        {
            return false;
        }

        options = new ImportOptions(
            CleanedFolder: cleanedFolder,
            ManifestPath: manifestPath,
            DatabasePath: databasePath);
        return true;
    }

    private static string ResolveCleanedFolder(string[] args)
    {
        if (TryGetOption(args, "--input", out var optionPath))
        {
            return Path.GetFullPath(optionPath);
        }

        var positional = GetPositionalArguments(args);
        if (positional.Count > 0)
        {
            return Path.GetFullPath(positional[0]);
        }

        Console.Write("Enter cleaned folder path: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? string.Empty : Path.GetFullPath(input.Trim());
    }

    private static string ResolveDatabasePath(string[] args, string cleanedFolder)
    {
        if (TryGetOption(args, "--db", out var optionPath))
        {
            return NormalizeDatabasePath(optionPath, cleanedFolder);
        }

        var positional = GetPositionalArguments(args);
        if (positional.Count > 1)
        {
            return NormalizeDatabasePath(positional[1], cleanedFolder);
        }

        Console.Write("Database folder or file path (leave empty to use cleaned folder): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return NormalizeDatabasePath(cleanedFolder, cleanedFolder);
        }

        return NormalizeDatabasePath(input.Trim(), cleanedFolder);
    }

    internal static string NormalizeDatabasePath(string inputPath, string cleanedFolder)
    {
        var candidate = string.IsNullOrWhiteSpace(inputPath) ? cleanedFolder : inputPath;
        var expanded = Path.GetFullPath(candidate);

        var extension = Path.GetExtension(expanded);
        if (IsDatabaseFileExtension(extension))
        {
            return expanded;
        }

        return Path.Combine(expanded, DefaultDatabaseFileName);
    }

    private static bool PrepareDatabaseLocation(string[] args, string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("Unable to resolve database directory.");
            return false;
        }

        Directory.CreateDirectory(directory);

        if (!File.Exists(databasePath))
        {
            return true;
        }

        var overwrite = ResolveOverwritePreference(args);
        if (!overwrite)
        {
            Console.WriteLine($"Database already exists: {databasePath}");
            Console.WriteLine("Import canceled. Use --overwrite true or choose another path.");
            return false;
        }

        File.Delete(databasePath);
        return true;
    }

    private static bool ResolveOverwritePreference(string[] args)
    {
        if (TryGetOption(args, "--overwrite", out var optionValue) && TryParseBoolean(optionValue, out var parsed))
        {
            return parsed;
        }

        Console.Write("Database exists. Overwrite? [Y/n]: ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        return TryParseBoolean(input, out var interactive) ? interactive : true;
    }

    private static bool TryGetOption(string[] args, string optionName, out string value)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }
            }

            if (arg.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = arg[(optionName.Length + 1)..];
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static List<string> GetPositionalArguments(string[] args)
    {
        var result = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                result.Add(arg);
                continue;
            }

            if ((arg.Equals("--input", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--db", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--overwrite", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length
                && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                i++;
            }
        }

        return result;
    }

    private static bool TryParseBoolean(string input, out bool value)
    {
        var normalized = input.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("no", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("n", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }

    internal static bool IsDatabaseFileExtension(string extension)
    {
        return extension.Equals(".db", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sqlite3", StringComparison.OrdinalIgnoreCase);
    }
}
