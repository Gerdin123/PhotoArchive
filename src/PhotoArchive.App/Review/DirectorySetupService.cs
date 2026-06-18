using Microsoft.EntityFrameworkCore;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;
using PhotoArchive.Infrastructure.Manifest;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.App.Review;

public sealed class DirectorySetupService
{
    public async Task<DirectorySetupResult> OpenOrPreprocessAsync(
        string inputRoot,
        string outputRoot,
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        var fullInputRoot = Path.GetFullPath(inputRoot);
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var fullDatabasePath = Path.GetFullPath(databasePath);

        if (!Directory.Exists(fullInputRoot))
        {
            throw new DirectoryNotFoundException($"Input folder does not exist: {fullInputRoot}");
        }

        await using var existingDbContext = PhotoArchiveDbContextFactory.Create(fullDatabasePath);
        await existingDbContext.Database.MigrateAsync(cancellationToken);
        var existingCount = await existingDbContext.ArchiveFiles.CountAsync(cancellationToken);
        if (existingCount > 0)
        {
            return new DirectorySetupResult(
                Preprocessed: false,
                FileCount: existingCount,
                DatabasePath: fullDatabasePath,
                OutputRoot: fullOutputRoot,
                Message: "Existing processed database opened.");
        }

        var scanner = new FileSystemScanner();
        var classifier = new SimpleFileClassifier();
        var hashService = new FileSystemHashService();
        var metadataReader = new ExifToolMetadataReader(new FileSystemMetadataReader());
        var dateInference = new DateInferenceService();
        var analyzedFiles = new List<AnalyzedFile>();

        await foreach (var scannedFile in scanner.ScanAsync(fullInputRoot, cancellationToken))
        {
            var classification = await classifier.ClassifyAsync(scannedFile, cancellationToken);
            var hash = await hashService.ComputeSha256Async(scannedFile.FullPath, cancellationToken);
            var evidence = await metadataReader.ReadDateEvidenceAsync(scannedFile, cancellationToken);
            var inferredDate = dateInference.Infer(evidence);
            analyzedFiles.Add(new AnalyzedFile(scannedFile, classification.MediaKind, hash, evidence, inferredDate));
        }

        var settings = new PreprocessingSettings(
            InputRoot: fullInputRoot,
            OutputRoot: fullOutputRoot,
            Execute: true);
        var plan = new OutputPlanner().CreatePlan(new PreprocessingRun(settings, DateTimeOffset.UtcNow, analyzedFiles));
        var validationErrors = new OutputPlanValidator().Validate(plan);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationErrors));
        }

        await new PreprocessingManifestWriter().WriteAsync(plan, cancellationToken);
        await new OperationLogWriter().WriteAsync(plan, cancellationToken);

        var executedOperations = await new ArchiveExecutor(hashService).ExecuteAsync(plan, cancellationToken);
        plan = plan with { Operations = executedOperations };
        await new PreprocessingManifestWriter().WriteAsync(plan, cancellationToken);
        await new OperationLogWriter().WriteAsync(plan, cancellationToken);

        await new PreprocessingPlanImporter().ImportAsync(existingDbContext, plan, cancellationToken);
        return new DirectorySetupResult(
            Preprocessed: true,
            FileCount: analyzedFiles.Count,
            DatabasePath: fullDatabasePath,
            OutputRoot: fullOutputRoot,
            Message: "Directory preprocessed and opened.");
    }
}
