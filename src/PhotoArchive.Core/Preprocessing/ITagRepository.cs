using PhotoArchive.Core.Domain;

namespace PhotoArchive.Core.Preprocessing;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken cancellationToken = default);
    Task<Tag> SaveAsync(Tag tag, CancellationToken cancellationToken = default);
}
