using System.Globalization;

namespace PhotoArchive.Cleaner.Services;

internal static class FolderDateResolver
{
    public static bool TryGetDateFromFolder(
        string sourceRoot,
        string filePath,
        DateTime fallbackDate,
        out DateTime parsedDate,
        out string source)
    {
        parsedDate = default;
        source = string.Empty;
        var sourceRootFullPath = Path.GetFullPath(sourceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var current = new DirectoryInfo(directoryPath);
        while (current != null)
        {
            if (TryParseDatePrefix(current.Name, out var partialDate))
            {
                parsedDate = ComposeDateFromPartial(partialDate, fallbackDate);
                source = $"FolderNamePrefix({partialDate.Pattern})";
                return true;
            }

            var currentFullPath = current.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (currentFullPath.Equals(sourceRootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = current.Parent;
        }

        return false;
    }

    internal static bool TryParseDatePrefix(string folderName, out PartialFolderDate parsedDate)
    {
        parsedDate = default;
        if (folderName.Length >= 8 && TryParseLeadingDigits(folderName, 8, out var yyyymmdd))
        {
            if (DateTime.TryParseExact(
                yyyymmdd,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
            {
                parsedDate = new PartialFolderDate(exactDate.Year, exactDate.Month, exactDate.Day, "yyyyMMdd");
                return true;
            }
        }

        if (folderName.Length >= 6 && TryParseLeadingDigits(folderName, 6, out var yyyymm))
        {
            if (DateTime.TryParseExact(
                yyyymm,
                "yyyyMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var yearMonthDate))
            {
                parsedDate = new PartialFolderDate(yearMonthDate.Year, yearMonthDate.Month, null, "yyyyMM");
                return true;
            }
        }

        if (folderName.Length >= 4 && TryParseLeadingDigits(folderName, 4, out var yyyy))
        {
            if (int.TryParse(yyyy, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1000 and <= 9999)
            {
                parsedDate = new PartialFolderDate(year, null, null, "yyyy");
                return true;
            }
        }

        return false;
    }

    private static bool TryParseLeadingDigits(string input, int count, out string digits)
    {
        digits = string.Empty;
        if (input.Length < count)
        {
            return false;
        }

        var prefix = input[..count];
        for (var i = 0; i < prefix.Length; i++)
        {
            if (!char.IsDigit(prefix[i]))
            {
                return false;
            }
        }

        digits = prefix;
        return true;
    }

    internal static DateTime ComposeDateFromPartial(PartialFolderDate folderDate, DateTime fallbackDate)
    {
        var month = folderDate.Month ?? fallbackDate.Month;
        month = Math.Clamp(month, 1, 12);

        var day = folderDate.Day ?? fallbackDate.Day;
        var maxDay = DateTime.DaysInMonth(folderDate.Year, month);
        day = Math.Clamp(day, 1, maxDay);

        return new DateTime(
            folderDate.Year,
            month,
            day,
            fallbackDate.Hour,
            fallbackDate.Minute,
            fallbackDate.Second,
            fallbackDate.Kind);
    }
}

internal readonly record struct PartialFolderDate(int Year, int? Month, int? Day, string Pattern);
