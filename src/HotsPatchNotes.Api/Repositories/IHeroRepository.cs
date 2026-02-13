using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository interface for Hero data access operations.
/// </summary>
public interface IHeroRepository
{
    /// <summary>
    /// Gets all heroes with optional filtering.
    /// </summary>
    Task<List<Hero>> GetAllAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a hero by short name with abilities and talents.
    /// </summary>
    Task<Hero?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct expanded roles.
    /// </summary>
    Task<List<string>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a hero exists by short name.
    /// </summary>
    Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default);
}
