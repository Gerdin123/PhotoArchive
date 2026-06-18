namespace PhotoArchive.Core.Domain;

public sealed class Tag
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public TagType Type { get; set; } = TagType.Custom;
}
