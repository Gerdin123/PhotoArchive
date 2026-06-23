using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;
using PhotoArchive.Infrastructure.Manifest;
using PhotoArchive.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.Infrastructure.Metadata;

var options = CliOptions.Parse(args);
if (options is null)
{
    CliOptions.PrintUsage();
    return 1;
}

if (options.WriteMetadata)
{
    if (string.IsNullOrWhiteSpace(options.DatabasePath))
    {
        Console.Error.WriteLine("--db is required with --write-metadata.");
        return 1;
    }

    await using var metadataDbContext = PhotoArchiveDbContextFactory.Create(Path.GetFullPath(options.DatabasePath));
    await metadataDbContext.Database.MigrateAsync();
    var result = await new MetadataWriteBackService(new XmpSidecarMetadataWriter())
        .WriteAsync(metadataDbContext, onlyCorrected: !options.WriteAllMetadata);

    Console.WriteLine($"Database:  {Path.GetFullPath(options.DatabasePath)}");
    Console.WriteLine("Mode:      metadata write-back");
    Console.WriteLine("Target:    XMP sidecars");
    Console.WriteLine($"Attempted: {result.Attempted}");
    Console.WriteLine($"Written:   {result.Written}");
    Console.WriteLine($"Skipped:   {result.Skipped}");
    Console.WriteLine($"Failed:    {result.Failed}");
    return result.Failed == 0 ? 0 : 4;
}

var settings = new PreprocessingSettings(
    InputRoot: Path.GetFullPath(options.InputRoot),
    OutputRoot: Path.GetFullPath(options.OutputRoot),
    Execute: options.Execute,
    AllowOutputInsideInput: options.AllowOutputInsideInput);

if (!Directory.Exists(settings.InputRoot))
{
    Console.Error.WriteLine($"Input folder does not exist: {settings.InputRoot}");
    return 1;
}

var runStartedAtUtc = DateTimeOffset.UtcNow;
var scanner = new FileSystemScanner();
var classifier = new SimpleFileClassifier();
var hashService = new FileSystemHashService();
var metadataReader = new ExifToolMetadataReader(new FileSystemMetadataReader());
var dateInference = new DateInferenceService();
var planner = new OutputPlanner();
var validator = new OutputPlanValidator();
var manifestWriter = new PreprocessingManifestWriter();
var operationLogWriter = new OperationLogWriter();
var executor = new ArchiveExecutor(hashService);

Console.WriteLine($"Input:  {settings.InputRoot}");
Console.WriteLine($"Output: {settings.OutputRoot}");
Console.WriteLine(settings.Execute ? "Mode:   execute" : "Mode:   dry-run");

var analyzedFiles = new List<AnalyzedFile>();
var skippedFiles = 0;
await foreach (var scannedFile in scanner.ScanAsync(settings.InputRoot))
{
    if (PreprocessingFileFilter.ShouldSkip(scannedFile))
    {
        skippedFiles++;
        continue;
    }

    var classification = await classifier.ClassifyAsync(scannedFile);
    var hash = await hashService.ComputeSha256Async(scannedFile.FullPath);
    var dateEvidence = await metadataReader.ReadDateEvidenceAsync(scannedFile);
    var inferredDate = dateInference.Infer(dateEvidence);

    analyzedFiles.Add(new AnalyzedFile(
        ScannedFile: scannedFile,
        MediaKind: classification.MediaKind,
        Sha256Hash: hash,
        DateEvidence: dateEvidence,
        DateInference: inferredDate));
}

var run = new PreprocessingRun(settings, runStartedAtUtc, analyzedFiles);
var plan = planner.CreatePlan(run);
var validationErrors = validator.Validate(plan);
if (validationErrors.Count > 0)
{
    Console.Error.WriteLine("Plan validation failed:");
    foreach (var error in validationErrors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    return 2;
}

var manifestPath = await manifestWriter.WriteAsync(plan);
var operationLogPath = await operationLogWriter.WriteAsync(plan);

if (settings.Execute)
{
    var executedOperations = await executor.ExecuteAsync(plan);
    plan = plan with { Operations = executedOperations };
    manifestPath = await manifestWriter.WriteAsync(plan);
    operationLogPath = await operationLogWriter.WriteAsync(plan);
}

if (!string.IsNullOrWhiteSpace(options.DatabasePath))
{
    await using var dbContext = PhotoArchiveDbContextFactory.Create(Path.GetFullPath(options.DatabasePath));
    await dbContext.Database.MigrateAsync();
    var importResult = await new PreprocessingPlanImporter().ImportAsync(dbContext, plan);
    Console.WriteLine($"Database:      {Path.GetFullPath(options.DatabasePath)}");
    Console.WriteLine($"DB files:      {importResult.ArchiveFiles}");
    Console.WriteLine($"DB metadata:   {importResult.MetadataRows}");
    Console.WriteLine($"DB duplicates: {importResult.DuplicateGroups}");
    Console.WriteLine($"DB operations: {importResult.OperationLogs}");
}

Console.WriteLine($"Files scanned: {analyzedFiles.Count}");
Console.WriteLine($"Files skipped: {skippedFiles}");
Console.WriteLine($"Manifest:      {manifestPath}");
Console.WriteLine($"Operation log: {operationLogPath}");

var failedCount = plan.Operations.Count(operation => operation.ExecutionResult == "Failed");
if (failedCount > 0)
{
    Console.Error.WriteLine($"Completed with {failedCount} failed operation(s). See operation log.");
    return 3;
}

return 0;

internal sealed record CliOptions(
    string InputRoot,
    string OutputRoot,
    bool Execute,
    bool AllowOutputInsideInput,
    string? DatabasePath,
    bool WriteMetadata,
    bool WriteAllMetadata)
{
    public static CliOptions? Parse(string[] args)
    {
        var input = GetOption(args, "--input");
        var output = GetOption(args, "--output");
        var databasePath = GetOption(args, "--db");
        var positional = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToList();
        var writeMetadata = HasFlag(args, "--write-metadata");

        input ??= positional.Count > 0 ? positional[0] : null;
        if (string.IsNullOrWhiteSpace(input) && !writeMetadata)
        {
            return null;
        }

        output ??= positional.Count > 1 ? positional[1] : CreateDefaultOutputRoot(input ?? ".");

        return new CliOptions(
            InputRoot: input ?? ".",
            OutputRoot: output,
            Execute: HasFlag(args, "--execute"),
            AllowOutputInsideInput: HasFlag(args, "--allow-output-inside-input"),
            DatabasePath: databasePath,
            WriteMetadata: writeMetadata,
            WriteAllMetadata: HasFlag(args, "--write-all-metadata"));
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  PhotoArchive.Cli --input <folder> [--output <folder>] [--db <sqlite-file>] [--execute]");
        Console.WriteLine("  PhotoArchive.Cli --write-metadata --db <sqlite-file> [--write-all-metadata]");
        Console.WriteLine();
        Console.WriteLine("Without --execute, the command writes a dry-run manifest and operation log only.");
        Console.WriteLine("Metadata write-back writes XMP sidecars and defaults to manually corrected photos only.");
    }

    private static string CreateDefaultOutputRoot(string input)
    {
        var fullInput = Path.GetFullPath(input);
        var parent = Directory.GetParent(fullInput)?.FullName ?? fullInput;
        var name = Path.GetFileName(fullInput.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(parent, $"{name}_archive");
    }

    private static bool HasFlag(string[] args, string flag)
    {
        return args.Any(arg => arg.Equals(flag, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }
        }

        return null;
    }
}
