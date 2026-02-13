using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for Hero business operations.
/// </summary>
public interface IHeroService
{
    /// <summary>
    /// Gets all heroes as summary DTOs.
    /// </summary>
    Task<List<HeroSummaryDto>> GetHeroesAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a hero detail by short name.
    /// </summary>
    Task<HeroDetailDto?> GetHeroAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct roles.
    /// </summary>
    Task<List<string>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all patches that affected a specific hero.
    /// </summary>
    Task<List<HeroPatchDto>> GetHeroPatchesAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all builds for a hero.
    /// </summary>
    Task<List<HeroBuildDto>> GetHeroBuildsAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new build for a hero.
    /// </summary>
    Task<HeroBuildDto?> CreateBuildAsync(string shortName, CreateBuildDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a build code and returns talent selections.
    /// </summary>
    ParsedBuildResult? ParseBuildCode(string code);
}

/// <summary>
/// Result of parsing a build code.
/// </summary>
public record ParsedBuildResult(
    string TalentCode,
    string? HeroShortName,
    Dictionary<int, int> Talents);
