using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Controllers;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Tests.Controllers;

/// <summary>
/// Tests for SyncController endpoints.
/// </summary>
public sealed class SyncControllerTests
{
    private readonly Mock<IGitHubSyncService> _mockSyncService;
    private readonly SyncController _controller;

    public SyncControllerTests()
    {
        _mockSyncService = new Mock<IGitHubSyncService>();
        _controller = new SyncController(_mockSyncService.Object, NullLogger<SyncController>.Instance);
    }

    [Fact]
    public async Task SyncAllAsync_SuccessfulSync_ReturnsOkWithResult()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = true,
            Message = "Sync complete",
            HeroesUpdated = 5,
            PatchesUpdated = 10,
            SyncedAt = DateTime.UtcNow,
            Errors = []
        };

        _mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncAllAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var syncResult = Assert.IsType<SyncResultDto>(okResult.Value);

        Assert.True(syncResult.Success);
        Assert.Equal(5, syncResult.HeroesUpdated);
        Assert.Equal(10, syncResult.PatchesUpdated);
        Assert.Empty(syncResult.Errors);

        _mockSyncService.Verify(s => s.SyncAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncAllAsync_SyncWithErrors_Returns207MultiStatus()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = false,
            Message = "Sync completed with errors",
            HeroesUpdated = 3,
            PatchesUpdated = 5,
            SyncedAt = DateTime.UtcNow,
            Errors = ["Failed to sync hero X", "Failed to fetch patch Y"]
        };

        _mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncAllAsync(CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(207, statusCodeResult.StatusCode); // Multi-Status

        var syncResult = Assert.IsType<SyncResultDto>(statusCodeResult.Value);
        Assert.False(syncResult.Success);
        Assert.Equal(2, syncResult.Errors.Count);
        Assert.Contains("Failed to sync hero X", syncResult.Errors);
    }

    [Fact]
    public async Task SyncAllAsync_PassesCancellationToken_ToService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var expectedResult = new SyncResultDto { Success = true };
        _mockSyncService
            .Setup(s => s.SyncAllAsync(token))
            .ReturnsAsync(expectedResult);

        // Act
        await _controller.SyncAllAsync(token);

        // Assert
        _mockSyncService.Verify(s => s.SyncAllAsync(token), Times.Once);
    }

    [Fact]
    public async Task SyncHeroesAsync_SuccessfulSync_ReturnsOkResult()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = true,
            Message = "Synced 10 heroes",
            HeroesUpdated = 10,
            SyncedAt = DateTime.UtcNow,
            Errors = []
        };

        _mockSyncService
            .Setup(s => s.SyncHeroesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncHeroesAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var syncResult = Assert.IsType<SyncResultDto>(okResult.Value);

        Assert.True(syncResult.Success);
        Assert.Equal(10, syncResult.HeroesUpdated);

        _mockSyncService.Verify(s => s.SyncHeroesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncHeroesAsync_SyncFails_Returns207MultiStatus()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = false,
            Message = "Failed to sync",
            Errors = ["Network error"]
        };

        _mockSyncService
            .Setup(s => s.SyncHeroesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncHeroesAsync(CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(207, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task SyncPatchesFromGitHubAsync_SuccessfulSync_ReturnsOkResult()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = true,
            Message = "Synced 20 patches",
            PatchesUpdated = 20,
            SyncedAt = DateTime.UtcNow,
            Errors = []
        };

        _mockSyncService
            .Setup(s => s.SyncPatchesFromGitHubAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncPatchesFromGitHubAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var syncResult = Assert.IsType<SyncResultDto>(okResult.Value);

        Assert.True(syncResult.Success);
        Assert.Equal(20, syncResult.PatchesUpdated);

        _mockSyncService.Verify(s => s.SyncPatchesFromGitHubAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncPatchesFromGitHubAsync_SyncFails_Returns207MultiStatus()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = false,
            Errors = ["GitHub API error"]
        };

        _mockSyncService
            .Setup(s => s.SyncPatchesFromGitHubAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncPatchesFromGitHubAsync(CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(207, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task SyncPatchesFromBlueTrackerAsync_SuccessfulSync_ReturnsOkResult()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = true,
            Message = "Synced 5 patches from BlueTracker",
            PatchesUpdated = 5,
            SyncedAt = DateTime.UtcNow,
            Errors = []
        };

        _mockSyncService
            .Setup(s => s.SyncPatchesFromBlueTrackerAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncPatchesFromBlueTrackerAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var syncResult = Assert.IsType<SyncResultDto>(okResult.Value);

        Assert.True(syncResult.Success);
        Assert.Equal(5, syncResult.PatchesUpdated);

        // Verify isInitialSync is false (subsequent sync behavior)
        _mockSyncService.Verify(
            s => s.SyncPatchesFromBlueTrackerAsync(false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncPatchesFromBlueTrackerAsync_SyncFails_Returns207MultiStatus()
    {
        // Arrange
        var expectedResult = new SyncResultDto
        {
            Success = false,
            Errors = ["BlueTracker scraping failed"]
        };

        _mockSyncService
            .Setup(s => s.SyncPatchesFromBlueTrackerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.SyncPatchesFromBlueTrackerAsync(CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(207, statusCodeResult.StatusCode);
    }
}
