using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for Patch business operations.
/// </summary>
public interface IPatchService
{
    /// <summary>
    /// Gets patches with optional filtering and pagination.
    /// </summary>
    Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(
        string? patchType = null,
        string? source = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a patch detail by internal ID.
    /// </summary>
    Task<ReconstructedPatchDto?> GetPatchAsync(string internalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct patch types.
    /// </summary>
    Task<List<string>> GetPatchTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all distinct data sources.
    /// </summary>
    Task<List<string>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets sections for a specific patch.
    /// </summary>
    Task<List<PatchSectionDto>> GetSectionsAsync(
        string internalId,
        string? sectionType = null,
        string? entityName = null,
        CancellationToken cancellationToken = default);
}
