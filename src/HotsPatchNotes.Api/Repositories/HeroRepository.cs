using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository implementation for Hero data access.
/// </summary>
public sealed class HeroRepository(HotsDbContext context) : IHeroRepository
{
    public async Task<List<Hero>> GetAllAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Heroes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(h => h.ExpandedRole == role || h.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(h => h.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h => h.Name.Contains(search) || h.ShortName.Contains(search));
        }

        return await query
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Hero?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var shortNameLower = shortName.ToLowerInvariant();
        return await context.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == shortNameLower, cancellationToken);
    }

    public async Task<List<string>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return await context.Heroes
            .Where(h => h.ExpandedRole != null)
            .Select(h => h.ExpandedRole!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var shortNameLower = shortName.ToLowerInvariant();
        return await context.Heroes.AnyAsync(h => h.ShortName == shortNameLower, cancellationToken);
    }
}
