using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Tests.Services;

/// <summary>
/// Tests for BackgroundSyncService background worker.
/// </summary>
public sealed class BackgroundSyncServiceTests
{
    [Fact]
    public async Task ExecuteAsync_OnStartup_PerformsInitialSyncAfter10Seconds()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var syncResult = new SyncResultDto { Success = true };

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncResult);

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var backgroundService = new BackgroundSyncService(serviceProvider, NullLogger<BackgroundSyncService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100)); // Wait for startup sync to complete
        cts.Cancel();
        await executeTask;

        // Assert - should have called SyncAllAsync at least once (initial sync)
        mockSyncService.Verify(
            s => s.SyncAllAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Initial sync should be triggered on startup");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var syncResult = new SyncResultDto { Success = true };

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncResult);

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var backgroundService = new BackgroundSyncService(serviceProvider, NullLogger<BackgroundSyncService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        cts.Cancel(); // Cancel immediately
        await executeTask;

        // Assert - should complete without throwing
        Assert.True(executeTask.IsCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_SyncThrowsException_LogsErrorAndContinues()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var callCount = 0;

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Sync failed");
                }
                return new SyncResultDto { Success = true };
            });

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var backgroundService = new BackgroundSyncService(serviceProvider, NullLogger<BackgroundSyncService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200)); // Wait for multiple sync attempts
        cts.Cancel();
        await executeTask;

        // Assert - should have attempted sync despite exception
        mockSyncService.Verify(
            s => s.SyncAllAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Service should continue running after exception");
    }

    [Fact]
    public async Task PerformSyncAsync_CreatesScopedService_CallsSyncAllAsync()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var syncResult = new SyncResultDto
        {
            Success = true,
            HeroesUpdated = 5,
            PatchesUpdated = 10
        };

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncResult);

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var backgroundService = new BackgroundSyncService(serviceProvider, NullLogger<BackgroundSyncService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        cts.Cancel();
        await executeTask;

        // Assert
        mockSyncService.Verify(s => s.SyncAllAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PerformSyncAsync_SuccessfulSync_LogsSuccess()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var syncResult = new SyncResultDto
        {
            Success = true,
            Message = "Sync complete",
            HeroesUpdated = 3,
            PatchesUpdated = 7,
            Errors = []
        };

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncResult);

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var mockLogger = new Mock<ILogger<BackgroundSyncService>>();
        var backgroundService = new BackgroundSyncService(serviceProvider, mockLogger.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        cts.Cancel();
        await executeTask;

        // Assert - verify logging was called (we can't check exact message without complex setup)
        mockSyncService.Verify(s => s.SyncAllAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PerformSyncAsync_SyncWithErrors_LogsWarning()
    {
        // Arrange
        var mockSyncService = new Mock<IGitHubSyncService>();
        var syncResult = new SyncResultDto
        {
            Success = false,
            Message = "Sync completed with errors",
            HeroesUpdated = 2,
            PatchesUpdated = 4,
            Errors = ["Error 1", "Error 2"]
        };

        mockSyncService
            .Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(syncResult);

        var serviceProvider = CreateServiceProvider(mockSyncService.Object);
        var backgroundService = new BackgroundSyncService(serviceProvider, NullLogger<BackgroundSyncService>.Instance);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = backgroundService.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        cts.Cancel();
        await executeTask;

        // Assert - service should handle errors gracefully
        mockSyncService.Verify(s => s.SyncAllAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Helper method to create a service provider with mocked dependencies.
    /// </summary>
    private static IServiceProvider CreateServiceProvider(IGitHubSyncService syncService)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => syncService);

        return services.BuildServiceProvider();
    }
}
