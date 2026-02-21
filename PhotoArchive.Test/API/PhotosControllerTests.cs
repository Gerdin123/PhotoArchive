using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.API.Controllers;
using PhotoArchive.API.DTOs;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Test.Infrastructure;

namespace PhotoArchive.Test.API;

public class PhotosControllerTests
{
    [Fact]
    public async Task UpdatePhoto_AddsNewLinks_AndUpdatesGroupingDate()
    {
        using var scope = TestDbContextFactory.Create();

        scope.Context.Tags.AddRange(
            new Tag { Id = 1, Name = "Tag 1", NormalizedName = "tag 1" },
            new Tag { Id = 2, Name = "Tag 2", NormalizedName = "tag 2" });
        scope.Context.People.AddRange(
            new Person { Id = 3, Name = "Person 3", NormalizedName = "person 3" },
            new Person { Id = 4, Name = "Person 4", NormalizedName = "person 4" });

        scope.Context.Photos.Add(new Photo
        {
            Id = 10,
            SourcePath = "source.jpg",
            OutputPath = "output.jpg",
            FileName = "output.jpg",
            Extension = ".jpg",
            Sha256 = new string('B', 64),
            GroupingDate = new DateTime(2020, 1, 1),
            GroupingYear = 2020,
            GroupingDateSource = GroupingDateSource.FileCreationTime
        });

        scope.Context.PhotoTags.Add(new PhotoTag { PhotoId = 10, TagId = 1 });
        scope.Context.PhotoPeople.Add(new PhotoPerson { PhotoId = 10, PersonId = 3 });
        await scope.Context.SaveChangesAsync();

        var controller = new PhotosController(scope.Context);
        var newDate = new DateTime(2022, 3, 4, 0, 0, 0, DateTimeKind.Utc);

        var result = await controller.UpdatePhoto(10, new UpdatePhotoDto
        {
            GroupingDate = newDate,
            TagIds = [1, 2],
            PersonIds = [3, 4]
        });

        Assert.IsType<NoContentResult>(result);

        var photo = await scope.Context.Photos
            .Include(p => p.PhotoTags)
            .Include(p => p.PhotoPeople)
            .SingleAsync(p => p.Id == 10);

        Assert.Equal(newDate, photo.GroupingDate);
        Assert.Equal([1, 2], [.. photo.PhotoTags.Select(x => x.TagId).Order()]);
        Assert.Equal([3, 4], [.. photo.PhotoPeople.Select(x => x.PersonId).Order()]);
    }

    [Fact]
    public async Task UpdatePhoto_ReturnsNotFound_WhenPhotoDoesNotExist()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new PhotosController(scope.Context);

        var result = await controller.UpdatePhoto(999, new UpdatePhotoDto { GroupingDate = DateTime.UtcNow });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePhoto_ReturnsBadRequest_WhenTagIdsContainUnknownValue()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.People.Add(new Person { Id = 1, Name = "A", NormalizedName = "a" });
        scope.Context.Photos.Add(new Photo
        {
            Id = 10,
            SourcePath = "source.jpg",
            OutputPath = "output.jpg",
            FileName = "output.jpg",
            Extension = ".jpg",
            Sha256 = new string('B', 64),
            GroupingDate = new DateTime(2020, 1, 1),
            GroupingYear = 2020,
            GroupingDateSource = GroupingDateSource.FileCreationTime
        });
        await scope.Context.SaveChangesAsync();

        var controller = new PhotosController(scope.Context);

        var result = await controller.UpdatePhoto(10, new UpdatePhotoDto
        {
            GroupingDate = DateTime.UtcNow,
            TagIds = [999]
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdatePhoto_ReturnsBadRequest_WhenPersonIdsContainUnknownValue()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.Add(new Tag { Id = 1, Name = "A", NormalizedName = "a" });
        scope.Context.Photos.Add(new Photo
        {
            Id = 10,
            SourcePath = "source.jpg",
            OutputPath = "output.jpg",
            FileName = "output.jpg",
            Extension = ".jpg",
            Sha256 = new string('B', 64),
            GroupingDate = new DateTime(2020, 1, 1),
            GroupingYear = 2020,
            GroupingDateSource = GroupingDateSource.FileCreationTime
        });
        await scope.Context.SaveChangesAsync();

        var controller = new PhotosController(scope.Context);

        var result = await controller.UpdatePhoto(10, new UpdatePhotoDto
        {
            GroupingDate = DateTime.UtcNow,
            PersonIds = [999]
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenPhotoMissing()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new PhotosController(scope.Context);

        var result = await controller.GetById(999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AddPersonToPhoto_AddsLink_AndIsIdempotent()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Photos.Add(new Photo
        {
            Id = 1,
            SourcePath = "source.jpg",
            OutputPath = "output.jpg",
            FileName = "output.jpg",
            Extension = ".jpg",
            Sha256 = new string('C', 64),
            GroupingDate = new DateTime(2020, 1, 1),
            GroupingYear = 2020,
            GroupingDateSource = GroupingDateSource.FileCreationTime
        });
        scope.Context.People.Add(new Person { Id = 2, Name = "Person", NormalizedName = "person" });
        await scope.Context.SaveChangesAsync();

        var controller = new PhotosController(scope.Context);

        var first = await controller.AddPersonToPhoto(1, 2);
        var second = await controller.AddPersonToPhoto(1, 2);
        var links = await scope.Context.PhotoPeople.Where(x => x.PhotoId == 1 && x.PersonId == 2).ToListAsync();

        Assert.IsType<NoContentResult>(first);
        Assert.IsType<NoContentResult>(second);
        Assert.Single(links);
    }

    [Fact]
    public async Task AddTagToPhoto_ReturnsNotFound_WhenTagMissing()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Photos.Add(new Photo
        {
            Id = 1,
            SourcePath = "source.jpg",
            OutputPath = "output.jpg",
            FileName = "output.jpg",
            Extension = ".jpg",
            Sha256 = new string('D', 64),
            GroupingDate = new DateTime(2020, 1, 1),
            GroupingYear = 2020,
            GroupingDateSource = GroupingDateSource.FileCreationTime
        });
        await scope.Context.SaveChangesAsync();

        var controller = new PhotosController(scope.Context);

        var result = await controller.AddTagToPhoto(1, 999);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
