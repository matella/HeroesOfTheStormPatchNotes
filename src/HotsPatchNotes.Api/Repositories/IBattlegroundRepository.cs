using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository interface for Battleground data access operations.
/// </summary>
public interface IBattlegroundRepository
{
    /// <summary>
    /// Gets all battlegrounds with optional filtering.
    /// </summary>
    Task<List<Battleground>> GetAllAsync(
        bool? inRotation = null,
        string? universe = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a battleground by short name.
    /// </summary>
    Task<Battleground?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a battleground exists by short name.
    /// </summary>
    Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new battleground.
    /// </summary>
    Task<Battleground> CreateAsync(Battleground battleground, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a battleground.
    /// </summary>
    Task UpdateAsync(Battleground battleground, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patch sections for a battleground.
    /// </summary>
    Task<List<PatchSection>> GetPatchSectionsAsync(
        string battlegroundName,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the entity names of all map-type patch sections (for per-map change counts).
    /// </summary>
    Task<List<string>> GetMapSectionNamesAsync(CancellationToken cancellationToken = default);
}
