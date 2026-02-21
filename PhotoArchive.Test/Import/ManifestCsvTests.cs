using PhotoArchive.Import;

namespace PhotoArchive.Test.Import;

public class ManifestCsvTests
{
    [Fact]
    public void BuildHeaderIndex_TrimsBom_AndFindsColumnsCaseInsensitive()
    {
        var headers = new List<string> { "\uFEFFSourcePath", "OutputPath", "Sha256" };

        var index = ManifestCsv.BuildHeaderIndex(headers);

        Assert.Equal(0, index["sourcepath"]);
        Assert.Equal(1, index["OUTPUTPATH"]);
        Assert.Equal(2, index["Sha256"]);
    }

    [Fact]
    public void GetRequiredIndex_Throws_WhenColumnMissing()
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourcePath"] = 0
        };

        Assert.Throws<InvalidOperationException>(() => ManifestCsv.GetRequiredIndex(index, "Sha256"));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("n", false)]
    [InlineData("0", false)]
    public void TryParseBoolean_ParsesCommonValues(string value, bool expected)
    {
        var ok = ManifestCsv.TryParseBoolean(value, out var parsed);

        Assert.True(ok);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void CountDataRows_IgnoresHeaderAndBlankLines()
    {
        var file = Path.Combine(Path.GetTempPath(), $"manifest-{Guid.NewGuid():N}.csv");
        File.WriteAllLines(file,
        [
            "A,B",
            "1,2",
            "",
            "3,4"
        ]);

        try
        {
            var rows = ManifestCsv.CountDataRows(file);
            Assert.Equal(2, rows);
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
