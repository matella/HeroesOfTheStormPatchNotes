using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for parsing hero data directly from S2MA .stormmod MPQ archives
/// </summary>
public interface IS2MAHeroParserService
{
    /// <summary>
    /// Syncs hero abilities and talents by parsing S2MA .stormmod files from the jamiephan/HeroesOfTheStorm_S2MA repository
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync result with count and errors</returns>
    Task<SyncResultDto> SyncHeroesFromS2MAAsync(CancellationToken cancellationToken = default);
}
