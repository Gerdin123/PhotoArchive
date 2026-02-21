using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class FolderDateResolverTests
{
    [Theory]
    [InlineData("20240102_trip", 2024, 1, 2, "yyyyMMdd")]
    [InlineData("202401_archive", 2024, 1, null, "yyyyMM")]
    [InlineData("2024_photos", 2024, null, null, "yyyy")]
    public void TryParseDatePrefix_RecognizesSupportedPatterns(string folder, int year, int? month, int? day, string pattern)
    {
        var ok = FolderDateResolver.TryParseDatePrefix(folder, out var parsed);

        Assert.True(ok);
        Assert.Equal(year, parsed.Year);
        Assert.Equal(month, parsed.Month);
        Assert.Equal(day, parsed.Day);
        Assert.Equal(pattern, parsed.Pattern);
    }

    [Fact]
    public void ComposeDateFromPartial_UsesFallbackDayAndTime()
    {
        var fallback = new DateTime(2025, 12, 31, 9, 8, 7, DateTimeKind.Utc);
        var partial = new PartialFolderDate(2024, 2, null, "yyyyMM");

        var result = FolderDateResolver.ComposeDateFromPartial(partial, fallback);

        Assert.Equal(new DateTime(2024, 2, 29, 9, 8, 7, DateTimeKind.Utc), result);
    }

    [Fact]
    public void TryGetDateFromFolder_ResolvesNearestMatchingParent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"photoarchive-folderdate-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "202403_trip", "child");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "a.jpg");
        File.WriteAllText(file, "x");

        try
        {
            var fallback = new DateTime(2020, 4, 5, 6, 7, 8);
            var ok = FolderDateResolver.TryGetDateFromFolder(root, file, fallback, out var date, out var source);

            Assert.True(ok);
            Assert.Equal(2024, date.Year);
            Assert.Equal(3, date.Month);
            Assert.Equal(5, date.Day);
            Assert.Equal("FolderNamePrefix(yyyyMM)", source);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
