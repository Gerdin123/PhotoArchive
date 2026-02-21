using PhotoArchive.Domain.Entities;

namespace PhotoArchive.Test.Infrastructure;

public class DbContextModelTests
{
    [Fact]
    public void Model_ConfiguresCompositeKeys_ForJoinEntities()
    {
        using var scope = TestDbContextFactory.Create();

        var photoTagPk = scope.Context.Model.FindEntityType(typeof(PhotoTag))?.FindPrimaryKey();
        var photoPersonPk = scope.Context.Model.FindEntityType(typeof(PhotoPerson))?.FindPrimaryKey();

        Assert.NotNull(photoTagPk);
        Assert.Equal(["PhotoId", "TagId"], [.. photoTagPk!.Properties.Select(p => p.Name)]);

        Assert.NotNull(photoPersonPk);
        Assert.Equal(["PhotoId", "PersonId"], [.. photoPersonPk!.Properties.Select(p => p.Name)]);
    }

    [Fact]
    public void Model_ConfiguresUniqueIndexes_ForNormalizedNames()
    {
        using var scope = TestDbContextFactory.Create();

        var personEntity = scope.Context.Model.FindEntityType(typeof(Person));
        var tagEntity = scope.Context.Model.FindEntityType(typeof(Tag));

        var personIndex = personEntity?.GetIndexes().SingleOrDefault(i => i.Properties.Any(p => p.Name == "NormalizedName"));
        var tagIndex = tagEntity?.GetIndexes().SingleOrDefault(i => i.Properties.Any(p => p.Name == "NormalizedName"));

        Assert.NotNull(personIndex);
        Assert.True(personIndex!.IsUnique);

        Assert.NotNull(tagIndex);
        Assert.True(tagIndex!.IsUnique);
    }

    [Fact]
    public void Model_ConfiguresPhotoPropertyMaxLengths()
    {
        using var scope = TestDbContextFactory.Create();

        var photo = scope.Context.Model.FindEntityType(typeof(Photo));
        var sourcePath = photo?.FindProperty(nameof(Photo.SourcePath));
        var sha = photo?.FindProperty(nameof(Photo.Sha256));

        Assert.NotNull(sourcePath);
        Assert.Equal(2048, sourcePath!.GetMaxLength());

        Assert.NotNull(sha);
        Assert.Equal(64, sha!.GetMaxLength());
    }
}
