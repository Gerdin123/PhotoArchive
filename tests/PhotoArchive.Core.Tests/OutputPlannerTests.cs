using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Core.Tests;

public sealed class OutputPlannerTests
{
    private readonly OutputPlanner planner = new();

    [Fact]
    public void CreatePlan_renames_supported_images_by_day_sequence_and_strict_decade()
    {
        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\b.jpg", "hash-b", MediaKind.SupportedImage, new DateTimeOffset(2010, 5, 1, 12, 0, 0, TimeSpan.Zero)),
            File("E:\\input\\a.jpg", "hash-a", MediaKind.SupportedImage, new DateTimeOffset(2010, 5, 1, 10, 0, 0, TimeSpan.Zero))));

        Assert.Contains(plan.Operations, operation =>
            operation.DestinationPath.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100501 - 1.jpg"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Operations, operation =>
            operation.DestinationPath.EndsWith(Path.Combine("Photos", "2010-2019", "2010", "20100501 - 2.jpg"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreatePlan_routes_exact_duplicates_to_duplicates_with_canonical_link()
    {
        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\short.jpg", "same-hash", MediaKind.SupportedImage, new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            File("E:\\input\\longer\\duplicate.jpg", "same-hash", MediaKind.SupportedImage, new DateTimeOffset(1999, 1, 1, 0, 0, 0, TimeSpan.Zero))));

        var duplicate = Assert.Single(plan.Operations, operation => operation.IsDuplicate);
        Assert.Equal(MediaKind.Duplicate, duplicate.MediaKind);
        Assert.Equal("same-hash", duplicate.DuplicateGroupId);
        Assert.EndsWith(Path.Combine("Duplicates", "1990-1999", "1999", "short.jpg"), duplicate.DestinationPath);
        Assert.Equal("E:\\input\\longer\\duplicate.jpg", duplicate.CanonicalSourcePath);
    }

    [Fact]
    public void CreatePlan_preserves_unsupported_original_filename()
    {
        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\notes.txt", "txt-hash", MediaKind.Unsupported, new DateTimeOffset(2020, 8, 2, 0, 0, 0, TimeSpan.Zero))));

        var operation = Assert.Single(plan.Operations);
        Assert.EndsWith(Path.Combine("Unsupported", "2020-2029", "2020", "notes.txt"), operation.DestinationPath);
    }

    [Fact]
    public void CreatePlan_routes_unknown_date_supported_images_to_unknown_date_bucket_with_stable_name()
    {
        var plan = planner.CreatePlan(CreateRun(
            FileWithoutDate("E:\\input\\mystery.JPG", "abcdef1234567890", MediaKind.SupportedImage)));

        var operation = Assert.Single(plan.Operations);

        Assert.Equal(MediaKind.SupportedImage, operation.MediaKind);
        Assert.Equal(DateConfidence.Unknown, operation.DateConfidence);
        Assert.EndsWith(Path.Combine("Photos", "UnknownDate", "UnknownDate - abcdef123456.jpg"), operation.DestinationPath);
    }

    [Fact]
    public void CreatePlan_routes_unknown_date_duplicates_to_unknown_duplicate_bucket()
    {
        var plan = planner.CreatePlan(CreateRun(
            FileWithoutDate("E:\\input\\longer\\canonical.jpg", "same-hash", MediaKind.SupportedImage),
            FileWithoutDate("E:\\input\\dup.jpg", "same-hash", MediaKind.SupportedImage)));

        var duplicate = Assert.Single(plan.Operations, operation => operation.IsDuplicate);

        Assert.Equal(MediaKind.Duplicate, duplicate.MediaKind);
        Assert.EndsWith(Path.Combine("Duplicates", "UnknownDate", "dup.jpg"), duplicate.DestinationPath);
        Assert.Equal("E:\\input\\longer\\canonical.jpg", duplicate.CanonicalSourcePath);
    }

    [Fact]
    public void CreatePlan_selects_supported_image_as_canonical_over_unsupported_same_hash()
    {
        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\unsupported\\copy.bin", "same-hash", MediaKind.Unsupported, new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            File("E:\\input\\photo.jpg", "same-hash", MediaKind.SupportedImage, new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero))));

        var unsupportedDuplicate = Assert.Single(plan.Operations, operation => operation.SourcePath.EndsWith("copy.bin", StringComparison.OrdinalIgnoreCase));
        var supportedCanonical = Assert.Single(plan.Operations, operation => operation.SourcePath.EndsWith("photo.jpg", StringComparison.OrdinalIgnoreCase));

        Assert.True(unsupportedDuplicate.IsDuplicate);
        Assert.Equal(MediaKind.Duplicate, unsupportedDuplicate.MediaKind);
        Assert.Equal("E:\\input\\photo.jpg", unsupportedDuplicate.CanonicalSourcePath);
        Assert.False(supportedCanonical.IsDuplicate);
        Assert.Equal(MediaKind.SupportedImage, supportedCanonical.MediaKind);
    }

    [Fact]
    public void CreatePlan_prefers_more_complete_metadata_when_selecting_duplicate_canonical()
    {
        var exifCanonical = new AnalyzedFile(
            new ScannedFile("E:\\input\\a.jpg", "a.jpg", ".jpg", 1),
            MediaKind.SupportedImage,
            "same-hash",
            new DateInferenceEvidence("a.jpg", ExifDateTimeOriginal: new DateTimeOffset(2015, 1, 1, 1, 0, 0, TimeSpan.Zero)),
            new DateInferenceResult(new DateTimeOffset(2015, 1, 1, 1, 0, 0, TimeSpan.Zero), DateConfidence.High, "EXIF:DateTimeOriginal"));
        var fileSystemOnly = new AnalyzedFile(
            new ScannedFile("E:\\input\\deeper\\b.jpg", "b.jpg", ".jpg", 1),
            MediaKind.SupportedImage,
            "same-hash",
            new DateInferenceEvidence("b.jpg", FileCreatedDate: new DateTimeOffset(2015, 1, 1, 1, 0, 0, TimeSpan.Zero)),
            new DateInferenceResult(new DateTimeOffset(2015, 1, 1, 1, 0, 0, TimeSpan.Zero), DateConfidence.Low, "FileCreatedDate"));

        var plan = planner.CreatePlan(CreateRun(exifCanonical, fileSystemOnly));

        var duplicate = Assert.Single(plan.Operations, operation => operation.IsDuplicate);
        Assert.Equal("E:\\input\\a.jpg", duplicate.CanonicalSourcePath);
    }

    [Fact]
    public void CreatePlan_sequences_same_day_equal_timestamps_by_original_path()
    {
        var timestamp = new DateTimeOffset(2018, 6, 7, 8, 9, 10, TimeSpan.Zero);

        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\z.jpg", "hash-z", MediaKind.SupportedImage, timestamp),
            File("E:\\input\\a.jpg", "hash-a", MediaKind.SupportedImage, timestamp),
            File("E:\\input\\m.jpg", "hash-m", MediaKind.SupportedImage, timestamp)));

        Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2018", "20180607 - 1.jpg"),
            plan.Operations.Single(operation => operation.SourcePath.EndsWith("a.jpg", StringComparison.OrdinalIgnoreCase)).DestinationPath);
        Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2018", "20180607 - 2.jpg"),
            plan.Operations.Single(operation => operation.SourcePath.EndsWith("m.jpg", StringComparison.OrdinalIgnoreCase)).DestinationPath);
        Assert.EndsWith(Path.Combine("Photos", "2010-2019", "2018", "20180607 - 3.jpg"),
            plan.Operations.Single(operation => operation.SourcePath.EndsWith("z.jpg", StringComparison.OrdinalIgnoreCase)).DestinationPath);
    }

    [Fact]
    public void CreatePlan_resolves_planned_destination_collisions_deterministically()
    {
        var plan = planner.CreatePlan(CreateRun(
            File("E:\\input\\one\\notes.txt", "hash-1", MediaKind.Unsupported, new DateTimeOffset(2020, 8, 2, 0, 0, 0, TimeSpan.Zero)),
            File("E:\\input\\two\\notes.txt", "hash-2", MediaKind.Unsupported, new DateTimeOffset(2020, 8, 2, 0, 0, 0, TimeSpan.Zero))));

        Assert.Contains(plan.Operations, operation => operation.DestinationPath.EndsWith("notes.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Operations, operation => operation.DestinationPath.EndsWith("notes_1.txt", StringComparison.OrdinalIgnoreCase));
    }

    private static PreprocessingRun CreateRun(params AnalyzedFile[] files)
    {
        return new PreprocessingRun(
            new PreprocessingSettings("E:\\input", "E:\\output", Execute: false),
            new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero),
            files);
    }

    private static AnalyzedFile File(string path, string hash, MediaKind mediaKind, DateTimeOffset date)
    {
        return new AnalyzedFile(
            new ScannedFile(path, Path.GetFileName(path), Path.GetExtension(path), 1),
            mediaKind,
            hash,
            new DateInferenceEvidence(Path.GetFileName(path), ExifDateTimeOriginal: date),
            new DateInferenceResult(date, DateConfidence.High, "EXIF:DateTimeOriginal"));
    }

    private static AnalyzedFile FileWithoutDate(string path, string hash, MediaKind mediaKind)
    {
        return new AnalyzedFile(
            new ScannedFile(path, Path.GetFileName(path), Path.GetExtension(path), 1),
            mediaKind,
            hash,
            new DateInferenceEvidence(Path.GetFileName(path)),
            new DateInferenceResult(null, DateConfidence.Unknown, "Unknown"));
    }
}
