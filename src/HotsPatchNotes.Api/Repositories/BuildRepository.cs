using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

/// <summary>
/// Repository implementation for HeroBuild data access.
/// </summary>
public sealed class BuildRepository(HotsDbContext context) : IBuildRepository
{
    public async Task<List<HeroBuild>> GetByHeroIdAsync(int heroId, CancellationToken cancellationToken = default)
    {
        return await context.HeroBuilds
            .Where(b => b.HeroId == heroId)
            .OrderByDescending(b => b.ViewCount)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<HeroBuild> CreateAsync(HeroBuild build, CancellationToken cancellationToken = default)
    {
        context.HeroBuilds.Add(build);
        await context.SaveChangesAsync(cancellationToken);
        return build;
    }

    public async Task<HeroBuild?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.HeroBuilds.FindAsync([id], cancellationToken);
    }
}
