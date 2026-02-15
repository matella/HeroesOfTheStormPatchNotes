using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for syncing comprehensive hero data from HeroesToolChest/heroes-data repository.
/// </summary>
public interface IHeroesDataSyncService
{
    /// <summary>
    /// Syncs hero data from the HeroesToolChest/heroes-data GitHub repository.
    /// This provides more comprehensive data than heroes-talents, including detailed stats,
    /// ability scaling, quest information, and more.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with hero count and any errors.</returns>
    Task<SyncResultDto> SyncHeroesDataAsync(CancellationToken cancellationToken = default);
}
