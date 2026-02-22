using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for enriching hero data from jamiephan/HeroesOfTheStorm_Gamedata raw XML files.
/// Provides Role, ExpandedRole, Type (Melee/Ranged), and ability Cooldown/Range/ManaCost.
/// </summary>
public interface IGamedataXmlEnrichmentService
{
    /// <summary>
    /// Enriches existing heroes in the database with data parsed from Gamedata XML files.
    /// </summary>
    Task<SyncResultDto> EnrichHeroesFromGamedataAsync(CancellationToken cancellationToken = default);
}
