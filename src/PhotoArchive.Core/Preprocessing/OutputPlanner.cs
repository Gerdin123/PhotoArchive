using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed class OutputPlanner : IOutputPlanner
{
    private readonly DecadeBucketPolicy decadeBucketPolicy;

    public OutputPlanner(DecadeBucketPolicy? decadeBucketPolicy = null)
    {
        this.decadeBucketPolicy = decadeBucketPolicy ?? DecadeBucketPolicy.Default;
    }

    public OutputPlan CreatePlan(PreprocessingRun run)
    {
        var canonicalByHash = SelectCanonicalFiles(run.Files);
        var daySequenceBySourcePath = BuildDaySequences(run.Files, canonicalByHash);
        var reservedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var operations = run.Files
            .OrderBy(file => file.ScannedFile.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(file => CreateOperation(run.Settings.OutputRoot, file, canonicalByHash, daySequenceBySourcePath, reservedDestinations))
            .ToList();

        return new OutputPlan(run.Settings, run.RunStartedAtUtc, operations);
    }

    private PlannedFileOperation CreateOperation(
        string outputRoot,
        AnalyzedFile file,
        IReadOnlyDictionary<string, AnalyzedFile> canonicalByHash,
        IReadOnlyDictionary<string, int> daySequenceBySourcePath,
        ISet<string> reservedDestinations)
    {
        var canonical = canonicalByHash[file.Sha256Hash];
        var isDuplicate = !string.Equals(
            file.ScannedFile.FullPath,
            canonical.ScannedFile.FullPath,
            StringComparison.OrdinalIgnoreCase);

        var mediaKind = isDuplicate ? MediaKind.Duplicate : file.MediaKind;
        var destination = mediaKind switch
        {
            MediaKind.SupportedImage => BuildPhotoDestination(outputRoot, file, daySequenceBySourcePath),
            MediaKind.Duplicate => BuildOriginalNameDestination(outputRoot, "Duplicates", file),
            _ => BuildOriginalNameDestination(outputRoot, "Unsupported", file)
        };

        destination = ReserveDestination(destination, reservedDestinations);

        return new PlannedFileOperation(
            SourcePath: file.ScannedFile.FullPath,
            DestinationPath: destination,
            MediaKind: mediaKind,
            Sha256Hash: file.Sha256Hash,
            InferredTakenDate: file.DateInference.TakenDate,
            DateConfidence: file.DateInference.Confidence,
            DateSource: file.DateInference.Source,
            IsDuplicate: isDuplicate,
            CanonicalSourcePath: isDuplicate ? canonical.ScannedFile.FullPath : null,
            DuplicateGroupId: isDuplicate ? file.Sha256Hash : null);
    }

    private string BuildPhotoDestination(
        string outputRoot,
        AnalyzedFile file,
        IReadOnlyDictionary<string, int> daySequenceBySourcePath)
    {
        var date = file.DateInference.TakenDate;
        var extension = NormalizeExtension(file.ScannedFile.Extension);

        if (date is null)
        {
            var unknownName = $"UnknownDate - {StableUnknownSequence(file)}{extension}";
            return Path.Combine(outputRoot, "Photos", "UnknownDate", unknownName);
        }

        var takenDate = date.Value;
        var sequence = daySequenceBySourcePath[file.ScannedFile.FullPath];
        var fileName = $"{takenDate:yyyyMMdd} - {sequence}{extension}";
        return Path.Combine(
            outputRoot,
            "Photos",
            decadeBucketPolicy.GetBucket(takenDate),
            takenDate.Year.ToString("0000"),
            fileName);
    }

    private string BuildOriginalNameDestination(string outputRoot, string bucket, AnalyzedFile file)
    {
        var date = file.DateInference.TakenDate;
        if (date is null)
        {
            return Path.Combine(outputRoot, bucket, "UnknownDate", file.ScannedFile.OriginalFileName);
        }

        return Path.Combine(
            outputRoot,
            bucket,
            decadeBucketPolicy.GetBucket(date),
            date.Value.Year.ToString("0000"),
            file.ScannedFile.OriginalFileName);
    }

    private static IReadOnlyDictionary<string, AnalyzedFile> SelectCanonicalFiles(IReadOnlyList<AnalyzedFile> files)
    {
        return files
            .GroupBy(file => file.Sha256Hash, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(file => file.MediaKind == MediaKind.SupportedImage)
                    .ThenByDescending(MetadataCompleteness)
                    .ThenByDescending(file => file.ScannedFile.FullPath.Length)
                    .ThenBy(file => file.ScannedFile.FullPath, StringComparer.OrdinalIgnoreCase)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, int> BuildDaySequences(
        IReadOnlyList<AnalyzedFile> files,
        IReadOnlyDictionary<string, AnalyzedFile> canonicalByHash)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var canonicalPhotos = files
            .Where(file => file.MediaKind == MediaKind.SupportedImage)
            .Where(file => canonicalByHash[file.Sha256Hash].ScannedFile.FullPath.Equals(
                file.ScannedFile.FullPath,
                StringComparison.OrdinalIgnoreCase))
            .Where(file => file.DateInference.TakenDate is not null)
            .OrderBy(file => file.DateInference.TakenDate)
            .ThenBy(file => file.ScannedFile.FullPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.Sha256Hash, StringComparer.OrdinalIgnoreCase)
            .GroupBy(file => DateOnly.FromDateTime(file.DateInference.TakenDate!.Value.Date));

        foreach (var dayGroup in canonicalPhotos)
        {
            var sequence = 1;
            foreach (var file in dayGroup)
            {
                result[file.ScannedFile.FullPath] = sequence;
                sequence++;
            }
        }

        return result;
    }

    private static int MetadataCompleteness(AnalyzedFile file)
    {
        var evidence = file.DateEvidence;
        var score = 0;
        if (evidence.ExifDateTimeOriginal is not null) score += 4;
        if (evidence.ExifCreateDate is not null) score += 3;
        if (evidence.XmpDateCreated is not null) score += 2;
        if (file.DateInference.TakenDate is not null) score += 1;
        return score;
    }

    private static string ReserveDestination(string destination, ISet<string> reservedDestinations)
    {
        var candidate = destination;
        var directory = Path.GetDirectoryName(destination) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        var index = 1;

        while (!reservedDestinations.Add(candidate))
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_{index}{extension}");
            index++;
        }

        return candidate;
    }

    private static string NormalizeExtension(string extension)
    {
        return extension.StartsWith(".", StringComparison.Ordinal) ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
    }

    private static string StableUnknownSequence(AnalyzedFile file)
    {
        return file.Sha256Hash[..Math.Min(12, file.Sha256Hash.Length)];
    }
}
