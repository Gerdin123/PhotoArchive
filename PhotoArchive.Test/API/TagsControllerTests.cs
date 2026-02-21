using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.API.Controllers;
using PhotoArchive.API.DTOs;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Test.Infrastructure;

namespace PhotoArchive.Test.API;

public class TagsControllerTests
{
    [Fact]
    public async Task Post_CreatesTag_WhenNameIsUnique()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new TagsController(scope.Context);

        var result = await controller.Post(new NameRequestDto { Name = "Travel" });
        var saved = await scope.Context.Tags.SingleAsync();

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<TagDto>(created.Value);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal(saved.Name, dto.Name);
        Assert.Equal("travel", saved.NormalizedName);
    }

    [Fact]
    public async Task Put_ReturnsNotFound_WhenTagDoesNotExist()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new TagsController(scope.Context);

        var result = await controller.Put(123, new NameRequestDto { Name = "Updated" });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Delete_RemovesExistingTag()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.Add(new Tag { Id = 2, Name = "Family", NormalizedName = "family" });
        await scope.Context.SaveChangesAsync();

        var controller = new TagsController(scope.Context);

        var result = await controller.Delete(2);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(scope.Context.Tags);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenNameAlreadyExists()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.Add(new Tag { Id = 1, Name = "Travel", NormalizedName = "travel" });
        await scope.Context.SaveChangesAsync();
        var controller = new TagsController(scope.Context);

        var result = await controller.Post(new NameRequestDto { Name = "TRAVEL" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsTagDto()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.Add(new Tag { Id = 5, Name = "Nature", NormalizedName = "nature" });
        await scope.Context.SaveChangesAsync();
        var controller = new TagsController(scope.Context);

        var result = await controller.Get(5);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TagDto>(ok.Value);
        Assert.Equal(5, dto.Id);
        Assert.Equal("Nature", dto.Name);
    }

    [Fact]
    public async Task Put_UpdatesAndReturnsDto()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.Add(new Tag { Id = 3, Name = "Old", NormalizedName = "old" });
        await scope.Context.SaveChangesAsync();
        var controller = new TagsController(scope.Context);

        var result = await controller.Put(3, new NameRequestDto { Name = "Updated" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<TagDto>(ok.Value);
        Assert.Equal(3, dto.Id);
        Assert.Equal("Updated", dto.Name);
    }

    [Fact]
    public async Task Get_ReturnsTagListAsDtos()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.Tags.AddRange(
            new Tag { Id = 1, Name = "One", NormalizedName = "one" },
            new Tag { Id = 2, Name = "Two", NormalizedName = "two" });
        await scope.Context.SaveChangesAsync();
        var controller = new TagsController(scope.Context);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<TagDto>>(ok.Value).ToList();
        Assert.Equal(2, dtos.Count);
        Assert.Contains(dtos, x => x.Name == "One");
        Assert.Contains(dtos, x => x.Name == "Two");
    }
}
