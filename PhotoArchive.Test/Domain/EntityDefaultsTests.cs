using PhotoArchive.Domain.Entities;

namespace PhotoArchive.Test.Domain;

public class EntityDefaultsTests
{
    [Fact]
    public void Photo_Collections_AreInitialized()
    {
        var photo = new Photo
        {
            SourcePath = "src.jpg",
            OutputPath = "out.jpg",
            FileName = "out.jpg",
            Extension = ".jpg",
            Sha256 = new string('A', 64)
        };

        Assert.NotNull(photo.PhotoTags);
        Assert.NotNull(photo.PhotoPeople);
        Assert.Empty(photo.PhotoTags);
        Assert.Empty(photo.PhotoPeople);
    }

    [Fact]
    public void Person_And_Tag_Collections_AreInitialized()
    {
        var person = new Person { Name = "Alice", NormalizedName = "alice" };
        var tag = new Tag { Name = "Travel", NormalizedName = "travel" };

        Assert.NotNull(person.PhotoPeople);
        Assert.Empty(person.PhotoPeople);
        Assert.NotNull(tag.PhotoTags);
        Assert.Empty(tag.PhotoTags);
    }

    [Fact]
    public void GroupingDateSource_ContainsExpectedValues()
    {
        var values = Enum.GetNames<GroupingDateSource>();

        Assert.Contains("DateTaken", values);
        Assert.Contains("FolderNamePrefix", values);
        Assert.Contains("FileCreationTime", values);
    }

    [Fact]
    public void PhotoBucket_ContainsExpectedValues()
    {
        var values = Enum.GetNames<PhotoBucket>();

        Assert.Contains("Images", values);
        Assert.Contains("Duplicates", values);
        Assert.Contains("Others", values);
    }
}
