namespace HotsPatchNotes.Api.Services;

public sealed class BackgroundSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundSyncService> _logger;
    private readonly TimeSpan _syncInterval;
    private readonly TimeSpan _initialDelay;

    public BackgroundSyncService(
        IServiceProvider serviceProvider,
        ILogger<BackgroundSyncService> logger,
        TimeSpan? syncInterval = null,
        TimeSpan? initialDelay = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _syncInterval = syncInterval ?? TimeSpan.FromHours(6);
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background sync service started. Sync interval: {Interval}", _syncInterval);

        // Initial sync on startup (with a small delay to let the app start)
        await Task.Delay(_initialDelay, stoppingToken);
        await PerformSyncAsync(stoppingToken);

        // Periodic sync
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_syncInterval, stoppingToken);
                await PerformSyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic sync");
            }
        }

        _logger.LogInformation("Background sync service stopped");
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled data sync...");

        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();

        try
        {
            var result = await syncService.SyncAllAsync(cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Scheduled sync completed successfully. Heroes: {Heroes}, Patches: {Patches}",
                    result.HeroesUpdated, result.PatchesUpdated);
            }
            else
            {
                _logger.LogWarning(
                    "Scheduled sync completed with errors. Heroes: {Heroes}, Patches: {Patches}, Errors: {ErrorCount}",
                    result.HeroesUpdated, result.PatchesUpdated, result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform scheduled sync");
        }
    }
}
