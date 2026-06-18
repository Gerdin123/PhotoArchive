using PhotoArchive.Core.Preprocessing;

namespace PhotoArchive.Core.Tests;

public sealed class DecadeBucketPolicyTests
{
    [Theory]
    [InlineData(1800, "1800-1809")]
    [InlineData(1899, "1890-1899")]
    [InlineData(1900, "1900-1909")]
    [InlineData(1999, "1990-1999")]
    [InlineData(2000, "2000-2009")]
    [InlineData(2009, "2000-2009")]
    [InlineData(2010, "2010-2019")]
    [InlineData(2019, "2010-2019")]
    [InlineData(2020, "2020-2029")]
    [InlineData(2099, "2090-2099")]
    [InlineData(2100, "2100-2109")]
    public void Policy_groups_years_by_strict_calendar_decade(int year, string expected)
    {
        var bucket = DecadeBucketPolicy.Default.GetBucket(new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(expected, bucket);
    }

    [Fact]
    public void Unknown_date_uses_unknown_bucket()
    {
        Assert.Equal("UnknownDate", DecadeBucketPolicy.Default.GetBucket(null));
    }
}
