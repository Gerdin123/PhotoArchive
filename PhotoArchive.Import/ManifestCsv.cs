using System.Globalization;

namespace PhotoArchive.Import;

internal static class ManifestCsv
{
    internal static int CountDataRows(string manifestPath)
    {
        using var reader = new StreamReader(manifestPath);
        _ = reader.ReadLine();

        var rows = 0;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                rows++;
            }
        }

        return rows;
    }

    internal static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = headers[i].Trim().TrimStart('\uFEFF');
            if (!index.ContainsKey(normalized))
            {
                index[normalized] = i;
            }
        }

        return index;
    }

    internal static int GetRequiredIndex(IReadOnlyDictionary<string, int> index, string key)
    {
        if (!index.TryGetValue(key, out var value))
        {
            throw new InvalidOperationException($"The CSV is missing required column '{key}'.");
        }

        return value;
    }

    internal static string GetField(IReadOnlyList<string> fields, int index)
    {
        if (index < 0 || index >= fields.Count)
        {
            return string.Empty;
        }

        return fields[index].Trim();
    }

    internal static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }

                continue;
            }

            if (ch == ',' && !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    internal static bool ParseBoolean(string input)
    {
        return TryParseBoolean(input, out var value) && value;
    }

    internal static bool TryParseBoolean(string input, out bool value)
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

    internal static bool TryParseInt(string input, out int value)
    {
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    internal static bool TryParseLong(string input, out long value)
    {
        return long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    internal static bool TryParseDateTime(string input, out DateTime value)
    {
        return DateTime.TryParse(
            input,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces,
            out value);
    }

    internal static bool TryParseNullableDateTime(string input, out DateTime? value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = null;
            return true;
        }

        if (TryParseDateTime(input, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }
}
