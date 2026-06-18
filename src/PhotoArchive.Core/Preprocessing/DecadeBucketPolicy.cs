namespace PhotoArchive.Core.Preprocessing;

public sealed class DecadeBucketPolicy
{
    public static DecadeBucketPolicy Default { get; } = new();

    public string GetBucket(DateTimeOffset? date)
    {
        if (date is null)
        {
            return "UnknownDate";
        }

        var year = date.Value.Year;
        var start = year / 10 * 10;
        return $"{start:0000}-{start + 9:0000}";
    }
}
