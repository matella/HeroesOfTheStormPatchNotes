using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Controllers;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Tests.Controllers;

public class PatchesControllerTests : TestBase
{
    [Fact]
    public async Task GetPatches_ReturnsPagedPatches()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreatePatchService(context);
        var controller = new PatchesController(service);

        // Act
        var result = await controller.GetPatchesAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedResult = Assert.IsType<PagedResultDto<PatchSummaryDto>>(okResult.Value);
        Assert.Equal(2, pagedResult.TotalCount);
        Assert.Equal(2, pagedResult.Items.Count);
    }

    [Fact]
    public async Task GetPatches_FilterByType_ReturnsMatchingPatches()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreatePatchService(context);
        var controller = new PatchesController(service);

        // Act
        var result = await controller.GetPatchesAsync(patchType: "Balance Update");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedResult = Assert.IsType<PagedResultDto<PatchSummaryDto>>(okResult.Value);
        Assert.Single(pagedResult.Items);
        Assert.Equal("Balance Update", pagedResult.Items[0].PatchType);
    }

    [Fact]
    public async Task GetPatch_ExistingPatch_ReturnsPatchDetail()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreatePatchService(context);
        var controller = new PatchesController(service);

        // Act
        var result = await controller.GetPatchAsync("2023-12-05");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var patch = Assert.IsType<ReconstructedPatchDto>(okResult.Value);
        Assert.Equal("December 5, 2023 Patch", patch.PatchName);
    }

    [Fact]
    public async Task GetPatch_NonExistingPatch_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreatePatchService(context);
        var controller = new PatchesController(service);

        // Act
        var result = await controller.GetPatchAsync("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPatchTypes_ReturnsDistinctTypes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreatePatchService(context);
        var controller = new PatchesController(service);

        // Act
        var result = await controller.GetPatchTypesAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var types = Assert.IsType<List<string>>(okResult.Value);
        Assert.Equal(2, types.Count);
        Assert.Contains("Balance Update", types);
        Assert.Contains("Hotfix", types);
    }
}
