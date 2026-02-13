using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository implementation for Battleground data access.
/// </summary>
public sealed class BattlegroundRepository(HotsDbContext context) : IBattlegroundRepository
{
    public async Task<List<Battleground>> GetAllAsync(
        bool? inRotation = null,
        string? universe = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Battlegrounds.AsQueryable();

        if (inRotation.HasValue)
        {
            query = query.Where(b => b.IsInRotation == inRotation.Value);
        }

        if (!string.IsNullOrWhiteSpace(universe))
        {
            var universeLower = universe.ToLowerInvariant();
            query = query.Where(b => b.Universe != null && b.Universe.ToLower() == universeLower);
        }

        return await query
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Battleground?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var shortNameLower = shortName.ToLowerInvariant();
        return await context.Battlegrounds
            .FirstOrDefaultAsync(b => b.ShortName.ToLower() == shortNameLower, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var shortNameLower = shortName.ToLowerInvariant();
        return await context.Battlegrounds.AnyAsync(b => b.ShortName.ToLower() == shortNameLower, cancellationToken);
    }

    public async Task<Battleground> CreateAsync(Battleground battleground, CancellationToken cancellationToken = default)
    {
        context.Battlegrounds.Add(battleground);
        await context.SaveChangesAsync(cancellationToken);
        return battleground;
    }

    public async Task UpdateAsync(Battleground battleground, CancellationToken cancellationToken = default)
    {
        context.Battlegrounds.Update(battleground);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PatchSection>> GetPatchSectionsAsync(
        string battlegroundName,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var nameLower = battlegroundName.ToLowerInvariant();

        var query = context.PatchSections
            .Include(ps => ps.Patch)
            .Where(ps => ps.SectionType == Constants.SectionTypes.Battleground &&
                ps.EntityName.ToLower() == nameLower)
            .OrderByDescending(ps => ps.Patch!.LiveDate);

        if (limit.HasValue)
        {
            return await query.Take(limit.Value).ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }
}
