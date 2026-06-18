using System.Globalization;
using System.Text.RegularExpressions;
using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public sealed partial class DateInferenceService : IDateInferenceService
{
    public DateInferenceResult Infer(DateInferenceEvidence evidence)
    {
        if (IsPlausible(evidence.ExifDateTimeOriginal))
        {
            return new DateInferenceResult(evidence.ExifDateTimeOriginal, DateConfidence.High, "EXIF:DateTimeOriginal");
        }

        if (IsPlausible(evidence.ExifCreateDate))
        {
            return new DateInferenceResult(evidence.ExifCreateDate, DateConfidence.High, "EXIF:CreateDate");
        }

        if (IsPlausible(evidence.XmpDateCreated))
        {
            return new DateInferenceResult(evidence.XmpDateCreated, DateConfidence.High, "XMP:DateCreated");
        }

        var filenameDate = TryInferFromFileName(evidence.OriginalFileName);
        if (filenameDate is not null)
        {
            return new DateInferenceResult(filenameDate, DateConfidence.Medium, "Filename");
        }

        if (IsPlausible(evidence.FileCreatedDate))
        {
            return new DateInferenceResult(evidence.FileCreatedDate, DateConfidence.Low, "FileCreatedDate");
        }

        if (IsPlausible(evidence.FileModifiedDate))
        {
            return new DateInferenceResult(evidence.FileModifiedDate, DateConfidence.Low, "FileModifiedDate");
        }

        return new DateInferenceResult(null, DateConfidence.Unknown, "Unknown");
    }

    private static DateTimeOffset? TryInferFromFileName(string fileName)
    {
        var match = FileNameDateRegex().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var dateText = $"{match.Groups["year"].Value}{match.Groups["month"].Value}{match.Groups["day"].Value}";
        if (DateTimeOffset.TryParseExact(
            dateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool IsPlausible(DateTimeOffset? value)
    {
        return value is not null && value.Value.Year is >= 1800 and <= 2100;
    }

    [GeneratedRegex(@"(?<!\d)(?<year>19\d{2}|20\d{2})[-_ ]?(?<month>0[1-9]|1[0-2])[-_ ]?(?<day>0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex FileNameDateRegex();
}
