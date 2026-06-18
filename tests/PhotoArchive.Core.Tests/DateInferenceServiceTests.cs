using PhotoArchive.Core.Domain;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Core.Tests;

public sealed class DateInferenceServiceTests
{
    private readonly DateInferenceService service = new();

    [Fact]
    public void Infer_prefers_exif_original_date()
    {
        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "IMG_20200101.jpg",
            ExifDateTimeOriginal: new DateTimeOffset(2007, 4, 14, 10, 30, 0, TimeSpan.Zero),
            ExifCreateDate: new DateTimeOffset(2008, 1, 1, 0, 0, 0, TimeSpan.Zero),
            XmpDateCreated: new DateTimeOffset(2009, 1, 1, 0, 0, 0, TimeSpan.Zero),
            FileCreatedDate: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateTimeOffset(2007, 4, 14, 10, 30, 0, TimeSpan.Zero), result.TakenDate);
        Assert.Equal(DateConfidence.High, result.Confidence);
        Assert.Equal("EXIF:DateTimeOriginal", result.Source);
    }

    [Fact]
    public void Infer_uses_filename_date_with_medium_confidence_before_file_system_dates()
    {
        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "holiday-20070414-001.jpg",
            FileCreatedDate: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateOnly(2007, 4, 14), DateOnly.FromDateTime(result.TakenDate!.Value.Date));
        Assert.Equal(DateConfidence.Medium, result.Confidence);
        Assert.Equal("Filename", result.Source);
    }

    [Fact]
    public void Infer_marks_missing_dates_as_unknown()
    {
        var result = service.Infer(new DateInferenceEvidence(OriginalFileName: "scan.jpg"));

        Assert.Null(result.TakenDate);
        Assert.Equal(DateConfidence.Unknown, result.Confidence);
        Assert.Equal("Unknown", result.Source);
    }

    [Fact]
    public void Infer_ignores_implausible_exif_original_and_falls_back_to_exif_create_date()
    {
        var createDate = new DateTimeOffset(1998, 7, 6, 5, 4, 3, TimeSpan.Zero);

        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "IMG_19980706.jpg",
            ExifDateTimeOriginal: new DateTimeOffset(1799, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ExifCreateDate: createDate));

        Assert.Equal(createDate, result.TakenDate);
        Assert.Equal(DateConfidence.High, result.Confidence);
        Assert.Equal("EXIF:CreateDate", result.Source);
    }

    [Fact]
    public void Infer_uses_xmp_date_when_exif_dates_are_missing()
    {
        var xmpDate = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);

        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "IMG_20010203.jpg",
            XmpDateCreated: xmpDate));

        Assert.Equal(xmpDate, result.TakenDate);
        Assert.Equal(DateConfidence.High, result.Confidence);
        Assert.Equal("XMP:DateCreated", result.Source);
    }

    [Theory]
    [InlineData("IMG_20190229.jpg")]
    [InlineData("IMG_20191301.jpg")]
    [InlineData("IMG_20191232.jpg")]
    [InlineData("SCAN_1201901019.jpg")]
    public void Infer_ignores_invalid_or_embedded_filename_dates_and_uses_filesystem_date(string fileName)
    {
        var createdDate = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);

        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: fileName,
            FileCreatedDate: createdDate));

        Assert.Equal(createdDate, result.TakenDate);
        Assert.Equal(DateConfidence.Low, result.Confidence);
        Assert.Equal("FileCreatedDate", result.Source);
    }

    [Fact]
    public void Infer_uses_file_modified_date_when_created_date_is_missing()
    {
        var modifiedDate = new DateTimeOffset(2023, 12, 31, 22, 30, 0, TimeSpan.Zero);

        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "scan-without-date.jpg",
            FileModifiedDate: modifiedDate));

        Assert.Equal(modifiedDate, result.TakenDate);
        Assert.Equal(DateConfidence.Low, result.Confidence);
        Assert.Equal("FileModifiedDate", result.Source);
    }

    [Theory]
    [InlineData(1800, true)]
    [InlineData(2100, true)]
    [InlineData(1799, false)]
    [InlineData(2101, false)]
    public void Infer_applies_plausible_date_boundaries_to_evidence(int year, bool isPlausible)
    {
        var evidenceDate = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = service.Infer(new DateInferenceEvidence(
            OriginalFileName: "scan.jpg",
            ExifDateTimeOriginal: evidenceDate));

        if (isPlausible)
        {
            Assert.Equal(evidenceDate, result.TakenDate);
            Assert.Equal(DateConfidence.High, result.Confidence);
        }
        else
        {
            Assert.Null(result.TakenDate);
            Assert.Equal(DateConfidence.Unknown, result.Confidence);
        }
    }
}
