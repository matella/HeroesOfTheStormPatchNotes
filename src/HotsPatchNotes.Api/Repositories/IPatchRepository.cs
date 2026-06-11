using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository interface for Patch data access operations.
/// </summary>
public interface IPatchRepository
{
    /// <summary>
    /// Gets patches with optional filtering and pagination.
    /// </summary>
    Task<(List<Patch> Items, int TotalCount)> GetAllAsync(
        string? patchType = null,
        string? source = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a patch by internal ID with sections.
    /// </summary>
    Task<Patch?> GetByInternalIdAsync(string internalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets hero/map section counts for a set of patches (patch id -> counts).
    /// </summary>
    Task<Dictionary<int, PatchSectionCounts>> GetSectionCountsAsync(
        IReadOnlyCollection<int> patchIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct patch types.
    /// </summary>
    Task<List<string>> GetPatchTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct data sources.
    /// </summary>
    Task<List<string>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patch sections for a specific patch with optional filtering.
    /// </summary>
    Task<List<PatchSection>> GetSectionsAsync(
        string internalId,
        string? sectionType = null,
        string? entityName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patch sections for a specific hero.
    /// </summary>
    Task<List<PatchSection>> GetSectionsForHeroAsync(
        int heroId,
        string heroName,
        CancellationToken cancellationToken = default);
}

/// <summary>Per-patch counts of hero and map sections (patch-list summaries).</summary>
public sealed record PatchSectionCounts(int HeroCount, int MapCount);
