using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Controllers;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Tests.Controllers;

public class BattlegroundsControllerTests : TestBase
{
    [Fact]
    public async Task GetBattlegrounds_ReturnsAllBattlegrounds()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateBattlegroundService(context);
        var logger = NullLogger<BattlegroundsController>.Instance;
        var controller = new BattlegroundsController(service, logger);

        // Act
        var result = await controller.GetBattlegrounds();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var battlegrounds = Assert.IsType<List<BattlegroundSummaryDto>>(okResult.Value);
        Assert.Single(battlegrounds);
    }

    [Fact]
    public async Task GetBattleground_ExistingBattleground_ReturnsDetail()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateBattlegroundService(context);
        var logger = NullLogger<BattlegroundsController>.Instance;
        var controller = new BattlegroundsController(service, logger);

        // Act
        var result = await controller.GetBattleground("cursed-hollow");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var battleground = Assert.IsType<BattlegroundDetailDto>(okResult.Value);
        Assert.Equal("Cursed Hollow", battleground.Name);
    }

    [Fact]
    public async Task GetBattleground_NonExistingBattleground_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateBattlegroundService(context);
        var logger = NullLogger<BattlegroundsController>.Instance;
        var controller = new BattlegroundsController(service, logger);

        // Act
        var result = await controller.GetBattleground("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateBattleground_NewBattleground_ReturnsCreated()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateBattlegroundService(context);
        var logger = NullLogger<BattlegroundsController>.Instance;
        var controller = new BattlegroundsController(service, logger);
        var dto = new CreateBattlegroundDto
        {
            ShortName = "alterac-pass",
            Name = "Alterac Pass",
            MapType = "3-Lane",
            Description = "A winter battleground",
            IsInRotation = true
        };

        // Act
        var result = await controller.CreateBattleground(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var battleground = Assert.IsType<BattlegroundSummaryDto>(createdResult.Value);
        Assert.Equal("Alterac Pass", battleground.Name);
    }

    [Fact]
    public async Task CreateBattleground_ExistingShortName_ReturnsConflict()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateBattlegroundService(context);
        var logger = NullLogger<BattlegroundsController>.Instance;
        var controller = new BattlegroundsController(service, logger);
        var dto = new CreateBattlegroundDto
        {
            ShortName = "cursed-hollow", // Already exists
            Name = "Duplicate",
            IsInRotation = true
        };

        // Act
        var result = await controller.CreateBattleground(dto);

        // Assert
        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
