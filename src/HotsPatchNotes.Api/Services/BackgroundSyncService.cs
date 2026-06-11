using HotsPatchNotes.Api.Data;
using Microsoft.EntityFrameworkCore;

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
        // HotS patches are infrequent, so a daily refresh is plenty (override via SYNC_INTERVAL_HOURS).
        var envHours = Environment.GetEnvironmentVariable("SYNC_INTERVAL_HOURS");
        _syncInterval = syncInterval
            ?? (int.TryParse(envHours, out var h) && h > 0 ? TimeSpan.FromHours(h) : TimeSpan.FromHours(24));
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background sync service started. Sync interval: {Interval}", _syncInterval);

        // Initial sync on startup (with a small delay to let the app start) — but SKIP it when the
        // database already has data (it persists in the volume), so a restart doesn't re-fetch
        // everything. The periodic sync below still keeps it fresh.
        await Task.Delay(_initialDelay, stoppingToken);
        if (!await HasExistingDataAsync(stoppingToken))
        {
            _logger.LogInformation("No data yet — performing initial sync.");
            await PerformSyncAsync(stoppingToken);
        }
        else if (await IsDataStaleAsync(stoppingToken))
        {
            // The server only runs evenings: the 24h periodic timer below rarely fires before
            // shutdown, so a boot-time freshness check IS the daily refresh.
            _logger.LogInformation("Data is stale — performing boot-time refresh sync.");
            await PerformSyncAsync(stoppingToken);
        }
        else
        {
            _logger.LogInformation("Data present and fresh — skipping initial sync.");
        }

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

    private async Task<bool> HasExistingDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HotsDbContext>();
            // Both families must exist — heroes-only (an earlier partial sync) must NOT skip the
            // initial sync, or patch notes never get fetched.
            return await db.Heroes.AnyAsync(cancellationToken)
                && await db.Patches.AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check existing data; will sync to be safe.");
            return false;
        }
    }

    private async Task<bool> IsDataStaleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var staleHoursEnv = Environment.GetEnvironmentVariable("SYNC_STALE_HOURS");
            var staleAfter = TimeSpan.FromHours(
                int.TryParse(staleHoursEnv, out var h) && h > 0 ? h : 12);
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HotsDbContext>();
            var last = await db.Patches.MaxAsync(p => (DateTime?)p.LastSyncedAt, cancellationToken);
            return last is null || DateTime.UtcNow - last.Value > staleAfter;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check data freshness; refreshing to be safe.");
            return true;
        }
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
