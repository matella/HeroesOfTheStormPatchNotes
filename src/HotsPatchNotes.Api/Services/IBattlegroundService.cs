using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service interface for Battleground business operations.
/// </summary>
public interface IBattlegroundService
{
    /// <summary>
    /// Gets all battlegrounds as summary DTOs.
    /// </summary>
    Task<List<BattlegroundSummaryDto>> GetBattlegroundsAsync(
        bool? inRotation = null,
        string? universe = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a battleground detail by short name.
    /// </summary>
    Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new battleground.
    /// </summary>
    Task<BattlegroundSummaryDto?> CreateBattlegroundAsync(CreateBattlegroundDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing battleground.
    /// </summary>
    Task<bool> UpdateBattlegroundAsync(string shortName, CreateBattlegroundDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patch history for a battleground.
    /// </summary>
    Task<List<BattlegroundPatchDto>> GetBattlegroundPatchesAsync(string shortName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a battleground exists.
    /// </summary>
    Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default);
}
