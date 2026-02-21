using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class CleanerOptionsResolverTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("0", false)]
    [InlineData("n", false)]
    public void TryParseBooleanOption_ParsesKnownValues(string input, bool expected)
    {
        var ok = CleanerOptionsResolver.TryParseBooleanOption(input, out var value);

        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryParseBooleanOption_ReturnsFalse_ForUnknownValue()
    {
        var ok = CleanerOptionsResolver.TryParseBooleanOption("maybe", out _);

        Assert.False(ok);
    }

    [Fact]
    public void CreateOutputFolder_CreatesTimestampedFolder_WhenDefaultExists()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"photoarchive-clean-{Guid.NewGuid():N}");
        var source = Path.Combine(parent, "source");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(parent, "source_cleaned"));

        try
        {
            var output = CleanerOptionsResolver.CreateOutputFolder(source);

            Assert.NotEqual(Path.Combine(parent, "source_cleaned"), output);
            Assert.StartsWith(Path.Combine(parent, "source_cleaned_"), output, StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(output));
        }
        finally
        {
            if (Directory.Exists(parent))
                Directory.Delete(parent, recursive: true);
        }
    }
}
