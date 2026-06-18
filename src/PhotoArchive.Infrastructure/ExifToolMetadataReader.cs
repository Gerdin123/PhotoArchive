using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Infrastructure;

public sealed class ExifToolMetadataReader : IMetadataReader
{
    private readonly IMetadataReader fallbackReader;

    public ExifToolMetadataReader(IMetadataReader fallbackReader)
    {
        this.fallbackReader = fallbackReader;
    }

    public async Task<DateInferenceEvidence> ReadDateEvidenceAsync(
        ScannedFile file,
        CancellationToken cancellationToken = default)
    {
        var fallbackEvidence = await fallbackReader.ReadDateEvidenceAsync(file, cancellationToken);

        try
        {
            using var process = StartExifTool(file.FullPath);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return fallbackEvidence;
            }

            return ParseEvidence(file.OriginalFileName, output, fallbackEvidence);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or JsonException or FormatException)
        {
            return fallbackEvidence;
        }
    }

    private static Process StartExifTool(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "exiftool",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-json");
        startInfo.ArgumentList.Add("-DateTimeOriginal");
        startInfo.ArgumentList.Add("-CreateDate");
        startInfo.ArgumentList.Add("-DateCreated");
        startInfo.ArgumentList.Add(path);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start ExifTool.");
    }

    private static DateInferenceEvidence ParseEvidence(
        string originalFileName,
        string json,
        DateInferenceEvidence fallbackEvidence)
    {
        using var document = JsonDocument.Parse(json);
        var first = document.RootElement.EnumerateArray().FirstOrDefault();

        return new DateInferenceEvidence(
            OriginalFileName: originalFileName,
            ExifDateTimeOriginal: TryReadDate(first, "DateTimeOriginal"),
            ExifCreateDate: TryReadDate(first, "CreateDate"),
            XmpDateCreated: TryReadDate(first, "DateCreated"),
            FileCreatedDate: fallbackEvidence.FileCreatedDate,
            FileModifiedDate: fallbackEvidence.FileModifiedDate);
    }

    private static DateTimeOffset? TryReadDate(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[]
        {
            "yyyy:MM:dd HH:mm:ssK",
            "yyyy:MM:dd HH:mm:sszzz",
            "yyyy:MM:dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:sszzz",
            "yyyy-MM-ddTHH:mm:ss"
        };

        if (DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed)
            ? parsed
            : null;
    }
}
