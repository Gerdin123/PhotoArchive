using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PhotoArchive.App.Diagnostics;
using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;
using PhotoArchive.Infrastructure;
using PhotoArchive.Infrastructure.Manifest;
using PhotoArchive.Infrastructure.Persistence;

namespace PhotoArchive.App.Review;

public sealed class DirectorySetupService
{
    private readonly IApplicationLogger logger;

    public DirectorySetupService(IApplicationLogger? logger = null)
    {
        this.logger = logger ?? AppLog.Current;
    }

    public async Task<DirectorySetupResult> OpenOrPreprocessAsync(
        string inputRoot,
        string outputRoot,
        string databasePath,
        IProgress<DirectorySetupProgress>? progress = null,
        bool forceClean = false,
        CancellationToken cancellationToken = default)
    {
        var progressStartedAtUtc = DateTimeOffset.UtcNow;
        var fullInputRoot = Path.GetFullPath(inputRoot);
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        var fullDatabasePath = Path.GetFullPath(databasePath);

        if (!Directory.Exists(fullInputRoot))
        {
            logger.Warning(nameof(DirectorySetupService), $"Input folder does not exist: {fullInputRoot}");
            throw new DirectoryNotFoundException($"Input folder does not exist: {fullInputRoot}");
        }

        if (forceClean)
        {
            ValidateForceCleanPaths(fullInputRoot, fullOutputRoot, fullDatabasePath);
            SqliteConnection.ClearAllPools();
            progress?.Report(DirectorySetupProgress.Create(
                "Force clean",
                "Removing previous PhotoArchive output and database...",
                filesFound: 0,
                filesProcessed: 0,
                totalFiles: 1,
                progressStartedAtUtc));
            ForceClean(fullOutputRoot, fullDatabasePath);
            progress?.Report(DirectorySetupProgress.Create(
                "Force clean",
                "Previous PhotoArchive output and database removed.",
                filesFound: 0,
                filesProcessed: 1,
                totalFiles: 1,
                progressStartedAtUtc));
        }

        var databaseDirectory = Path.GetDirectoryName(fullDatabasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        logger.Info(nameof(DirectorySetupService), $"Opening input '{fullInputRoot}', output '{fullOutputRoot}', database '{fullDatabasePath}', forceClean={forceClean}.");
        progress?.Report(DirectorySetupProgress.Create(
            "Preparing",
            "Opening database...",
            filesFound: 0,
            filesProcessed: 0,
            totalFiles: 1,
            progressStartedAtUtc));

        await using var existingDbContext = PhotoArchiveDbContextFactory.Create(fullDatabasePath);
        await existingDbContext.Database.MigrateAsync(cancellationToken);
        var existingCount = await existingDbContext.ArchiveFiles.CountAsync(cancellationToken);
        progress?.Report(DirectorySetupProgress.Create(
            "Preparing",
            "Database opened.",
            filesFound: existingCount,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));
        if (existingCount > 0)
        {
            logger.Info(nameof(DirectorySetupService), $"Existing database opened with {existingCount} file(s).");
            progress?.Report(DirectorySetupProgress.Create(
                "Opened",
                "Existing database opened.",
                filesFound: existingCount,
                filesProcessed: existingCount,
                totalFiles: existingCount,
                progressStartedAtUtc));
            await GenerateThumbnailsAsync(
                existingDbContext,
                fullOutputRoot,
                progress,
                existingCount,
                existingCount,
                progressStartedAtUtc,
                cancellationToken);
            return await CreateResultAsync(
                existingDbContext,
                preprocessed: false,
                databasePath: fullDatabasePath,
                outputRoot: fullOutputRoot,
                message: "Existing processed database opened.",
                progressStartedAtUtc,
                cancellationToken);
        }

        var scanner = new FileSystemScanner();
        var classifier = new SimpleFileClassifier();
        var hashService = new FileSystemHashService();
        var metadataReader = new ExifToolMetadataReader(new FileSystemMetadataReader());
        var dateInference = new DateInferenceService();
        var analyzedFiles = new List<AnalyzedFile>();
        var allFiles = Directory.EnumerateFiles(fullInputRoot, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                Path = path,
                FileName = Path.GetFileName(path),
                Extension = Path.GetExtension(path)
            })
            .ToList();
        var totalFiles = allFiles.Count(file => !PreprocessingFileFilter.ShouldSkip(file.FileName, file.Extension));
        var skippedFiles = allFiles.Count - totalFiles;
        logger.Info(nameof(DirectorySetupService), $"Found {allFiles.Count} file(s), skipping {skippedFiles} thumbnail/system artifact(s), preprocessing {totalFiles} file(s).");
        progress?.Report(DirectorySetupProgress.Create(
            "Scanning",
            totalFiles == 0 ? $"No processable files found. Skipped {skippedFiles} thumbnail/system artifact(s)." : $"Found {totalFiles} processable file(s). Skipped {skippedFiles}.",
            filesFound: allFiles.Count,
            filesProcessed: allFiles.Count,
            totalFiles: allFiles.Count,
            progressStartedAtUtc));

        await foreach (var scannedFile in scanner.ScanAsync(fullInputRoot, cancellationToken))
        {
            if (PreprocessingFileFilter.ShouldSkip(scannedFile))
            {
                logger.Info(nameof(DirectorySetupService), $"Skipping thumbnail/system artifact '{scannedFile.FullPath}'.");
                continue;
            }

            var classification = await classifier.ClassifyAsync(scannedFile, cancellationToken);
            var hash = await hashService.ComputeSha256Async(scannedFile.FullPath, cancellationToken);
            var evidence = await metadataReader.ReadDateEvidenceAsync(scannedFile, cancellationToken);
            var inferredDate = dateInference.Infer(evidence);
            analyzedFiles.Add(new AnalyzedFile(scannedFile, classification.MediaKind, hash, evidence, inferredDate));
            progress?.Report(DirectorySetupProgress.Create(
                "Analyzing",
                $"Analyzed {analyzedFiles.Count} of {totalFiles}: {scannedFile.OriginalFileName}",
                filesFound: allFiles.Count,
                filesProcessed: analyzedFiles.Count,
                totalFiles,
                progressStartedAtUtc));
        }

        progress?.Report(DirectorySetupProgress.Create(
            "Planning",
            "Building deterministic output plan...",
            filesFound: allFiles.Count,
            filesProcessed: 0,
            totalFiles: 1,
            progressStartedAtUtc));
        var settings = new PreprocessingSettings(
            InputRoot: fullInputRoot,
            OutputRoot: fullOutputRoot,
            Execute: true);
        var plan = new OutputPlanner().CreatePlan(new PreprocessingRun(settings, DateTimeOffset.UtcNow, analyzedFiles));
        var validationErrors = new OutputPlanValidator().Validate(plan);
        if (validationErrors.Count > 0)
        {
            logger.Warning(nameof(DirectorySetupService), $"Plan validation failed with {validationErrors.Count} error(s).");
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationErrors));
        }
        progress?.Report(DirectorySetupProgress.Create(
            "Planning",
            "Output plan validated.",
            filesFound: allFiles.Count,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));

        progress?.Report(DirectorySetupProgress.Create(
            "Writing manifest",
            "Writing pre-copy manifest and operation log...",
            filesFound: allFiles.Count,
            filesProcessed: 0,
            totalFiles: 1,
            progressStartedAtUtc));
        await new PreprocessingManifestWriter().WriteAsync(plan, cancellationToken);
        await new OperationLogWriter().WriteAsync(plan, cancellationToken);
        progress?.Report(DirectorySetupProgress.Create(
            "Writing manifest",
            "Pre-copy manifest and operation log written.",
            filesFound: allFiles.Count,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));

        progress?.Report(DirectorySetupProgress.Create(
            "Copying",
            "Copying and verifying files...",
            filesFound: allFiles.Count,
            filesProcessed: 0,
            totalFiles: plan.Operations.Count,
            progressStartedAtUtc));
        var executedOperations = await new ArchiveExecutor(hashService).ExecuteAsync(
            plan,
            operationProgress => progress?.Report(DirectorySetupProgress.Create(
                "Copying",
                $"Copied and verified {operationProgress.CompletedOperations} of {operationProgress.TotalOperations} file(s).",
                filesFound: allFiles.Count,
                filesProcessed: operationProgress.CompletedOperations,
                totalFiles: operationProgress.TotalOperations,
                progressStartedAtUtc)),
            cancellationToken);
        plan = plan with { Operations = executedOperations };
        progress?.Report(DirectorySetupProgress.Create(
            "Writing final manifest",
            "Writing post-copy manifest and operation log...",
            filesFound: allFiles.Count,
            filesProcessed: 0,
            totalFiles: 1,
            progressStartedAtUtc));
        await new PreprocessingManifestWriter().WriteAsync(plan, cancellationToken);
        await new OperationLogWriter().WriteAsync(plan, cancellationToken);
        progress?.Report(DirectorySetupProgress.Create(
            "Writing final manifest",
            "Post-copy manifest and operation log written.",
            filesFound: allFiles.Count,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));

        progress?.Report(DirectorySetupProgress.Create(
            "Importing",
            "Importing preprocessing results into database...",
            filesFound: allFiles.Count,
            filesProcessed: 0,
            totalFiles: 1,
            progressStartedAtUtc));
        await new PreprocessingPlanImporter().ImportAsync(existingDbContext, plan, cancellationToken);
        progress?.Report(DirectorySetupProgress.Create(
            "Importing",
            "Preprocessing results imported into database.",
            filesFound: allFiles.Count,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));
        await GenerateThumbnailsAsync(
            existingDbContext,
            fullOutputRoot,
            progress,
            allFiles.Count,
            totalFiles,
            progressStartedAtUtc,
            cancellationToken);
        logger.Info(nameof(DirectorySetupService), $"Directory preprocessed with {analyzedFiles.Count} file(s).");
        progress?.Report(DirectorySetupProgress.Create(
            "Complete",
            $"Directory preprocessed. {analyzedFiles.Count} file(s). Skipped {skippedFiles} thumbnail/system artifact(s).",
            filesFound: allFiles.Count,
            filesProcessed: 1,
            totalFiles: 1,
            progressStartedAtUtc));
        return await CreateResultAsync(
            existingDbContext,
            preprocessed: true,
            databasePath: fullDatabasePath,
            outputRoot: fullOutputRoot,
            message: "Directory preprocessed and opened.",
            progressStartedAtUtc,
            cancellationToken);
    }

    private static async Task<DirectorySetupResult> CreateResultAsync(
        PhotoArchiveDbContext dbContext,
        bool preprocessed,
        string databasePath,
        string outputRoot,
        string message,
        DateTimeOffset progressStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var fileCount = await dbContext.ArchiveFiles.CountAsync(cancellationToken);
        var imagesLeft = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.SupportedImage
                && file.Status != ArchiveFileStatus.Duplicate
                && file.Status != ArchiveFileStatus.Deleted,
            cancellationToken);
        var duplicates = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.Duplicate || file.Status == ArchiveFileStatus.Duplicate,
            cancellationToken);
        var unsupported = await dbContext.ArchiveFiles.CountAsync(
            file => file.MediaKind == MediaKind.Unsupported || file.MediaKind == MediaKind.Unknown,
            cancellationToken);

        return new DirectorySetupResult(
            Preprocessed: preprocessed,
            FileCount: fileCount,
            ImagesLeft: imagesLeft,
            Duplicates: duplicates,
            UnsupportedFiles: unsupported,
            Elapsed: DateTimeOffset.UtcNow - progressStartedAtUtc,
            DatabasePath: databasePath,
            OutputRoot: outputRoot,
            Message: message);
    }

    private async Task GenerateThumbnailsAsync(
        PhotoArchiveDbContext dbContext,
        string outputRoot,
        IProgress<DirectorySetupProgress>? progress,
        int filesFound,
        int filesProcessed,
        DateTimeOffset progressStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var supportedFiles = await dbContext.ArchiveFiles
            .Where(file => file.MediaKind == MediaKind.SupportedImage
                && file.Status != ArchiveFileStatus.Duplicate
                && file.Status != ArchiveFileStatus.Deleted
                && file.CurrentPath != null)
            .OrderBy(file => file.OriginalPath)
            .ToListAsync(cancellationToken);
        var supportedFileIds = supportedFiles.Select(file => file.Id).ToArray();
        var metadataByFileId = await dbContext.PhotoMetadata
            .Where(metadata => supportedFileIds.Contains(metadata.ArchiveFileId))
            .ToDictionaryAsync(metadata => metadata.ArchiveFileId, cancellationToken);
        var candidates = supportedFiles
            .Where(file => file.ThumbnailStatus != ThumbnailStatus.Generated
                || string.IsNullOrWhiteSpace(file.ThumbnailPath)
                || !File.Exists(file.ThumbnailPath)
                || !metadataByFileId.TryGetValue(file.Id, out var metadata)
                || string.IsNullOrWhiteSpace(metadata.AverageColorHex)
                || string.IsNullOrWhiteSpace(metadata.PerceptualHash))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var thumbnailService = new AvaloniaThumbnailService();
        var thumbnailRoot = Path.Combine(outputRoot, "Thumbnails");
        for (var index = 0; index < candidates.Count; index++)
        {
            var file = candidates[index];
            progress?.Report(DirectorySetupProgress.Create(
                "Thumbnails",
                $"Generated {index} of {candidates.Count} thumbnail(s).",
                filesFound,
                filesProcessed: index,
                totalFiles: candidates.Count,
                progressStartedAtUtc));

            var thumbnailPath = Path.Combine(thumbnailRoot, $"{file.Id:N}.jpg");
            try
            {
                var analysis = await thumbnailService.CreateThumbnailWithAnalysisAsync(file.CurrentPath!, thumbnailPath, cancellationToken);
                file.ThumbnailPath = analysis.ThumbnailPath;
                file.ThumbnailStatus = ThumbnailStatus.Generated;
                if (metadataByFileId.TryGetValue(file.Id, out var metadata))
                {
                    metadata.AverageColorHex = analysis.AverageColorHex;
                    metadata.PerceptualHash = analysis.PerceptualHash;
                }

                dbContext.OperationLogs.Add(new OperationLog
                {
                    OperationType = "Thumbnail",
                    SourcePath = file.CurrentPath!,
                    DestinationPath = analysis.ThumbnailPath,
                    Result = "Generated"
                });
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or NullReferenceException
                or ArgumentException
                or InvalidOperationException)
            {
                file.ThumbnailPath = null;
                file.ThumbnailStatus = ThumbnailStatus.Failed;
                dbContext.OperationLogs.Add(new OperationLog
                {
                    OperationType = "Thumbnail",
                    SourcePath = file.CurrentPath!,
                    DestinationPath = thumbnailPath,
                    Result = "Failed",
                    ErrorMessage = ex.Message
                });
                logger.Warning(nameof(DirectorySetupService), $"Thumbnail generation failed for '{file.CurrentPath}': {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        progress?.Report(DirectorySetupProgress.Create(
            "Thumbnails",
            $"Processed thumbnail generation for {candidates.Count} supported image(s).",
            filesFound,
            filesProcessed: candidates.Count,
            totalFiles: candidates.Count,
            progressStartedAtUtc));
    }

    private void ForceClean(string outputRoot, string databasePath)
    {
        foreach (var managedDirectory in new[] { "Photos", "Duplicates", "Unsupported", "Manifests", "Thumbnails" })
        {
            var path = Path.Combine(outputRoot, managedDirectory);
            if (Directory.Exists(path))
            {
                logger.Warning(nameof(DirectorySetupService), $"Force-clean removing managed output directory '{path}'.");
                Directory.Delete(path, recursive: true);
            }
        }

        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                logger.Warning(nameof(DirectorySetupService), $"Force-clean removing database file '{path}'.");
                File.Delete(path);
            }
        }
    }

    private static void ValidateForceCleanPaths(string inputRoot, string outputRoot, string databasePath)
    {
        if (PathsOverlap(inputRoot, outputRoot))
        {
            throw new InvalidOperationException("Force-clean requires the cleaned output folder and original input folder to be separate, non-overlapping folders.");
        }

        if (IsSameOrSubPathOf(databasePath, inputRoot))
        {
            throw new InvalidOperationException("Force-clean requires the SQLite database to be outside the original input folder.");
        }
    }

    private static bool PathsOverlap(string firstPath, string secondPath)
    {
        return IsSameOrSubPathOf(firstPath, secondPath) || IsSameOrSubPathOf(secondPath, firstPath);
    }

    private static bool IsSameOrSubPathOf(string candidatePath, string parentPath)
    {
        var candidate = EnsureTrailingSeparator(Path.GetFullPath(candidatePath));
        var parent = EnsureTrailingSeparator(Path.GetFullPath(parentPath));
        return candidate.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(path);
        return trimmed + Path.DirectorySeparatorChar;
    }
}
