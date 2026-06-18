using PhotoArchive.Core.Domain;

namespace PhotoArchive.App.Review;

public sealed record TagOption(Guid Id, string Name, TagType Type)
{
    public string DisplayName => $"{Name} ({Type})";
}
