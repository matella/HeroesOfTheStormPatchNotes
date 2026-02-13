using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository implementation for Patch data access.
/// </summary>
public sealed class PatchRepository(HotsDbContext context) : IPatchRepository
{
    public async Task<(List<Patch> Items, int TotalCount)> GetAllAsync(
        string? patchType = null,
        string? source = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = context.Patches.AsQueryable();

        if (!string.IsNullOrWhiteSpace(patchType))
        {
            query = query.Where(p => p.PatchType == patchType);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(p => p.Source == source);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.LiveDate ?? DateTime.MinValue)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Patch?> GetByInternalIdAsync(string internalId, CancellationToken cancellationToken = default)
    {
        return await context.Patches
            .Include(p => p.Sections.OrderBy(s => s.Order))
            .ThenInclude(s => s.Hero)
            .FirstOrDefaultAsync(p => p.InternalId == internalId, cancellationToken);
    }

    public async Task<List<string>> GetPatchTypesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Patches
            .Where(p => p.PatchType != null)
            .Select(p => p.PatchType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Patches
            .Where(p => p.Source != null)
            .Select(p => p.Source!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PatchSection>> GetSectionsAsync(
        string internalId,
        string? sectionType = null,
        string? entityName = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.PatchSections
            .Include(s => s.Hero)
            .Include(s => s.Patch)
            .Where(s => s.Patch.InternalId == internalId);

        if (!string.IsNullOrWhiteSpace(sectionType))
        {
            query = query.Where(s => s.SectionType == sectionType);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(s => s.EntityName.Contains(entityName));
        }

        return await query
            .OrderBy(s => s.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PatchSection>> GetSectionsForHeroAsync(
        int heroId,
        string heroName,
        CancellationToken cancellationToken = default)
    {
        var heroNameLower = heroName.ToLowerInvariant();

        return await context.PatchSections
            .Include(s => s.Patch)
            .Where(s => s.HeroId == heroId || s.EntityName.ToLower() == heroNameLower)
            .OrderByDescending(s => s.Patch.LiveDate ?? DateTime.MinValue)
            .ThenByDescending(s => s.PatchId)
            .ThenBy(s => s.Order)
            .ToListAsync(cancellationToken);
    }
}
