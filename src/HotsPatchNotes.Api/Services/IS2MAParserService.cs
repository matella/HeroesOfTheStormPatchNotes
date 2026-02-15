using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for parsing S2MA (StarCraft II Archive) map files from HotS.
/// Provides authoritative battleground data directly from game files.
/// </summary>
public interface IS2MAParserService
{
    /// <summary>
    /// Syncs battleground data by parsing S2MA map files from jamiephan/HeroesOfTheStorm_S2MA repository.
    /// Provides more reliable data than web scraping, with fallback to Fandom wiki for descriptive content.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sync result with battleground count and any errors.</returns>
    Task<SyncResultDto> SyncBattlegroundsFromS2MAAsync(CancellationToken cancellationToken = default);
}
