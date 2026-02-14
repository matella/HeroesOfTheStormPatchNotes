using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Controllers;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Tests.Controllers;

public class HeroesControllerTests : TestBase
{
    [Fact]
    public async Task GetHeroes_ReturnsAllHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroesAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var heroes = Assert.IsType<List<HeroSummaryDto>>(okResult.Value);
        Assert.Equal(3, heroes.Count);
    }

    [Fact]
    public async Task GetHeroes_FilterByRole_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroesAsync(role: "Tank");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var heroes = Assert.IsType<List<HeroSummaryDto>>(okResult.Value);
        Assert.Single(heroes);
        Assert.Equal("Arthas", heroes[0].Name);
    }

    [Fact]
    public async Task GetHeroes_FilterByType_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroesAsync(type: "Ranged");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var heroes = Assert.IsType<List<HeroSummaryDto>>(okResult.Value);
        Assert.Single(heroes);
        Assert.Equal("Valla", heroes[0].Name);
    }

    [Fact]
    public async Task GetHeroes_Search_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroesAsync(search: "Art");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var heroes = Assert.IsType<List<HeroSummaryDto>>(okResult.Value);
        Assert.Single(heroes);
        Assert.Equal("Arthas", heroes[0].Name);
    }

    [Fact]
    public async Task GetHero_ExistingHero_ReturnsHeroDetail()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroAsync("arthas");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var hero = Assert.IsType<HeroDetailDto>(okResult.Value);
        Assert.Equal("Arthas", hero.Name);
        Assert.Equal("The Lich King", hero.Title);
        Assert.NotEmpty(hero.Abilities);
        Assert.NotEmpty(hero.Talents);
    }

    [Fact]
    public async Task GetHero_NonExistingHero_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroAsync("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRoles_ReturnsDistinctRoles()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetRolesAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var roles = Assert.IsType<List<string>>(okResult.Value);
        Assert.Equal(3, roles.Count);
        Assert.Contains("Tank", roles);
        Assert.Contains("Support", roles);
        Assert.Contains("Ranged Assassin", roles);
    }

    [Fact]
    public async Task GetHeroPatches_ExistingHero_ReturnsPatches()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroPatchesAsync("arthas");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var patches = Assert.IsType<List<HeroPatchDto>>(okResult.Value);
        Assert.NotEmpty(patches);
    }

    [Fact]
    public async Task GetHeroBuilds_ExistingHero_ReturnsBuilds()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = await controller.GetHeroBuildsAsync("arthas");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var builds = Assert.IsType<List<HeroBuildDto>>(okResult.Value);
        Assert.Empty(builds); // No builds seeded
    }

    [Fact]
    public async Task CreateBuild_ValidBuild_ReturnsBuild()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);
        var createDto = new CreateBuildDto
        {
            Name = "Tank Build",
            TalentCode = "1234512",
            Description = "Standard tank build"
        };

        // Act
        var result = await controller.CreateBuildAsync("arthas", createDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var build = Assert.IsType<HeroBuildDto>(createdResult.Value);
        Assert.Equal("Tank Build", build.Name);
        Assert.Equal("1234512", build.TalentCode);
        Assert.Equal("[T1234512,arthas]", build.FullCode);
    }

    [Fact]
    public async Task CreateBuild_InvalidTalentCode_ReturnsBadRequest()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);
        var createDto = new CreateBuildDto
        {
            Name = "Invalid Build",
            TalentCode = "123", // Too short
            Description = "Should fail"
        };

        // Act
        var result = await controller.CreateBuildAsync("arthas", createDto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void ParseBuildCode_ValidFullCode_ReturnsParsedResult()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = controller.ParseBuildCode("[T1234512,arthas]");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void ParseBuildCode_EmptyCode_ReturnsBadRequest()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var controller = new HeroesController(service, NullLogger<HeroesController>.Instance);

        // Act
        var result = controller.ParseBuildCode("");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
