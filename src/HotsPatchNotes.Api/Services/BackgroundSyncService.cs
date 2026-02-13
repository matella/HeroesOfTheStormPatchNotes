namespace HotsPatchNotes.Api.Services;

public sealed class BackgroundSyncService(IServiceProvider serviceProvider, ILogger<BackgroundSyncService> logger) : BackgroundService
{
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background sync service started. Sync interval: {Interval}", _syncInterval);

        // Initial sync on startup (with a small delay to let the app start)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
                logger.LogError(ex, "Error during periodic sync");
            }
        }

        logger.LogInformation("Background sync service stopped");
    }

    private async Task PerformSyncAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting scheduled data sync...");

        using var scope = serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();

        try
        {
            var result = await syncService.SyncAllAsync(cancellationToken);

            if (result.Success)
            {
                logger.LogInformation(
                    "Scheduled sync completed successfully. Heroes: {Heroes}, Patches: {Patches}",
                    result.HeroesUpdated, result.PatchesUpdated);
            }
            else
            {
                logger.LogWarning(
                    "Scheduled sync completed with errors. Heroes: {Heroes}, Patches: {Patches}, Errors: {ErrorCount}",
                    result.HeroesUpdated, result.PatchesUpdated, result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to perform scheduled sync");
        }
    }
}
