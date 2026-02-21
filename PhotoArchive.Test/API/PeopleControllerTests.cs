using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.API.Controllers;
using PhotoArchive.API.DTOs;
using PhotoArchive.Domain.Entities;
using PhotoArchive.Test.Infrastructure;

namespace PhotoArchive.Test.API;

public class PeopleControllerTests
{
    [Fact]
    public async Task Post_ReturnsBadRequest_WhenNameMissing()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new PeopleController(scope.Context);

        var result = await controller.Post(new NameRequestDto { Name = string.Empty });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Post_ReturnsBadRequest_WhenNormalizedNameAlreadyExists()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.People.Add(new Person { Id = 0, Name = "Alice", NormalizedName = "alice" });
        await scope.Context.SaveChangesAsync();

        var controller = new PeopleController(scope.Context);

        var result = await controller.Post(new NameRequestDto { Name = "ALICE" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Post_CreatesPerson_WithNormalizedName()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new PeopleController(scope.Context);

        var result = await controller.Post(new NameRequestDto { Name = "Bob" });
        var saved = await scope.Context.People.SingleAsync(p => p.Name == "Bob");

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<PersonDto>(created.Value);
        Assert.Equal(saved.Id, dto.Id);
        Assert.Equal("Bob", dto.Name);
        Assert.True(saved.Id > 0);
        Assert.Equal("bob", saved.NormalizedName);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenPersonMissing()
    {
        using var scope = TestDbContextFactory.Create();
        var controller = new PeopleController(scope.Context);

        var result = await controller.Delete(42);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Put_UpdatesAndReturnsDto()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.People.Add(new Person { Id = 6, Name = "Old", NormalizedName = "old" });
        await scope.Context.SaveChangesAsync();
        var controller = new PeopleController(scope.Context);

        var result = await controller.Put(6, new NameRequestDto { Name = "New Name" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PersonDto>(ok.Value);
        Assert.Equal(6, dto.Id);
        Assert.Equal("New Name", dto.Name);
    }

    [Fact]
    public async Task Put_ReturnsBadRequest_WhenNameAlreadyExists()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.People.AddRange(
            new Person { Id = 1, Name = "Alice", NormalizedName = "alice" },
            new Person { Id = 2, Name = "Bob", NormalizedName = "bob" });
        await scope.Context.SaveChangesAsync();
        var controller = new PeopleController(scope.Context);

        var result = await controller.Put(2, new NameRequestDto { Name = "ALICE" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Get_ReturnsPeopleOrderedByName()
    {
        using var scope = TestDbContextFactory.Create();
        scope.Context.People.AddRange(
            new Person { Id = 1, Name = "Zed", NormalizedName = "zed" },
            new Person { Id = 2, Name = "Amy", NormalizedName = "amy" });
        await scope.Context.SaveChangesAsync();
        var controller = new PeopleController(scope.Context);

        var result = (await controller.Get()).ToList();

        Assert.Equal(["Amy", "Zed"], result.Select(x => x.Name).ToArray());
    }
}
