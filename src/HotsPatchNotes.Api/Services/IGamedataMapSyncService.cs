using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service that syncs battleground metadata from the jamiephan/HeroesOfTheStorm_Gamedata repository.
/// Parses gamestrings.txt files to extract map loading screen data (names, objectives, descriptions).
/// </summary>
public interface IGamedataMapSyncService
{
    /// <summary>
    /// Syncs battleground metadata from gamestrings.txt files in the Gamedata repository.
    /// Fetches two files: the main heroesdata.stormmod file (14 maps) and the Alterac Pass
    /// per-map file. Creates or updates Battleground entities without overwriting wiki-enriched
    /// fields (ObjectiveTiming, MercCamps, BossInfo, Tips, ImageUrl).
    /// </summary>
    Task<SyncResultDto> SyncBattlegroundsFromGamedataAsync(CancellationToken cancellationToken = default);
}
