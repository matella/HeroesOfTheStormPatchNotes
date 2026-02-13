using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository interface for HeroBuild data access operations.
/// </summary>
public interface IBuildRepository
{
    /// <summary>
    /// Gets all builds for a hero.
    /// </summary>
    Task<List<HeroBuild>> GetByHeroIdAsync(int heroId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new build.
    /// </summary>
    Task<HeroBuild> CreateAsync(HeroBuild build, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a build by ID.
    /// </summary>
    Task<HeroBuild?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
