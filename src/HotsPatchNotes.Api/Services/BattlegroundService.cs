using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service implementation for Battleground business operations.
/// </summary>
public sealed class BattlegroundService(IBattlegroundRepository battlegroundRepository) : IBattlegroundService
{
    private const int RecentPatchesLimit = 10;

    public async Task<List<BattlegroundSummaryDto>> GetBattlegroundsAsync(
        bool? inRotation = null,
        string? universe = null,
        CancellationToken cancellationToken = default)
    {
        var battlegrounds = await battlegroundRepository.GetAllAsync(inRotation, universe, cancellationToken);

        return battlegrounds.Select(MapToSummaryDto).ToList();
    }

    public async Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var battleground = await battlegroundRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (battleground is null)
        {
            return null;
        }

        var patchSections = await battlegroundRepository.GetPatchSectionsAsync(
            battleground.Name,
            RecentPatchesLimit,
            cancellationToken);

        return MapToDetailDto(battleground, patchSections);
    }

    public async Task<BattlegroundSummaryDto?> CreateBattlegroundAsync(CreateBattlegroundDto dto, CancellationToken cancellationToken = default)
    {
        if (await battlegroundRepository.ExistsAsync(dto.ShortName, cancellationToken))
        {
            return null;
        }

        var battleground = new Battleground
        {
            ShortName = dto.ShortName,
            Name = dto.Name,
            MapType = dto.MapType,
            Description = dto.Description,
            Objective = dto.Objective,
            ObjectiveTiming = dto.ObjectiveTiming,
            MercCamps = dto.MercCamps,
            BossInfo = dto.BossInfo,
            Tips = dto.Tips,
            ImageUrl = dto.ImageUrl,
            IsInRotation = dto.IsInRotation,
            ReleaseDate = dto.ReleaseDate,
            Event = dto.Event,
            Universe = dto.Universe
        };

        await battlegroundRepository.CreateAsync(battleground, cancellationToken);

        return MapToSummaryDto(battleground);
    }

    public async Task<bool> UpdateBattlegroundAsync(string shortName, CreateBattlegroundDto dto, CancellationToken cancellationToken = default)
    {
        var battleground = await battlegroundRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (battleground is null)
        {
            return false;
        }

        battleground.Name = dto.Name;
        battleground.MapType = dto.MapType;
        battleground.Description = dto.Description;
        battleground.Objective = dto.Objective;
        battleground.ObjectiveTiming = dto.ObjectiveTiming;
        battleground.MercCamps = dto.MercCamps;
        battleground.BossInfo = dto.BossInfo;
        battleground.Tips = dto.Tips;
        battleground.ImageUrl = dto.ImageUrl;
        battleground.IsInRotation = dto.IsInRotation;
        battleground.ReleaseDate = dto.ReleaseDate;
        battleground.Event = dto.Event;
        battleground.Universe = dto.Universe;

        await battlegroundRepository.UpdateAsync(battleground, cancellationToken);

        return true;
    }

    public async Task<List<BattlegroundPatchDto>> GetBattlegroundPatchesAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var battleground = await battlegroundRepository.GetByShortNameAsync(shortName, cancellationToken);
        if (battleground is null)
        {
            return [];
        }

        var patchSections = await battlegroundRepository.GetPatchSectionsAsync(battleground.Name, null, cancellationToken);

        return patchSections.Select(ps => new BattlegroundPatchDto
        {
            PatchId = ps.PatchId,
            PatchName = ps.Patch!.PatchName,
            LiveDate = ps.Patch.LiveDate,
            Content = ps.Content
        }).ToList();
    }

    public async Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default)
    {
        return await battlegroundRepository.ExistsAsync(shortName, cancellationToken);
    }

    private static BattlegroundSummaryDto MapToSummaryDto(Battleground battleground)
    {
        return new BattlegroundSummaryDto
        {
            Id = battleground.Id,
            ShortName = battleground.ShortName,
            Name = battleground.Name,
            MapType = battleground.MapType,
            Description = battleground.Description,
            ImageUrl = battleground.ImageUrl,
            IsInRotation = battleground.IsInRotation,
            Universe = battleground.Universe
        };
    }

    private static BattlegroundDetailDto MapToDetailDto(Battleground battleground, List<PatchSection> patchSections)
    {
        return new BattlegroundDetailDto
        {
            Id = battleground.Id,
            ShortName = battleground.ShortName,
            Name = battleground.Name,
            MapType = battleground.MapType,
            Description = battleground.Description,
            Objective = battleground.Objective,
            ObjectiveTiming = battleground.ObjectiveTiming,
            MercCamps = battleground.MercCamps,
            BossInfo = battleground.BossInfo,
            Tips = battleground.Tips,
            ImageUrl = battleground.ImageUrl,
            IsInRotation = battleground.IsInRotation,
            ReleaseDate = battleground.ReleaseDate,
            Event = battleground.Event,
            Universe = battleground.Universe,
            RecentPatches = patchSections.Select(ps => new BattlegroundPatchDto
            {
                PatchId = ps.PatchId,
                PatchName = ps.Patch!.PatchName,
                LiveDate = ps.Patch.LiveDate,
                Content = ps.Content
            }).ToList()
        };
    }
}
