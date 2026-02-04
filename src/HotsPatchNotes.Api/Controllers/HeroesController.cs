using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HeroesController : ControllerBase
{
    private readonly HotsDbContext _dbContext;

    public HeroesController(HotsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Get all heroes with optional filtering.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<HeroSummaryDto>>> GetHeroes(
        [FromQuery] string? role = null,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null)
    {
        var query = _dbContext.Heroes.AsQueryable();

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

        var heroEntities = await query
            .OrderBy(h => h.Name)
            .ToListAsync();

        var heroes = heroEntities.Select(h => new HeroSummaryDto
        {
            Id = h.Id,
            ShortName = h.ShortName,
            Name = h.Name,
            Icon = h.Icon,
            Role = h.Role,
            ExpandedRole = h.ExpandedRole,
            Type = h.Type,
            ReleaseDate = h.ReleaseDate,
            Tags = h.TagsJson != null 
                ? JsonSerializer.Deserialize<List<string>>(h.TagsJson) ?? new List<string>() 
                : new List<string>()
        }).ToList();

        return Ok(heroes);
    }

    /// <summary>
    /// Get a specific hero by short name with all abilities and talents.
    /// </summary>
    [HttpGet("{shortName}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<HeroDetailDto>> GetHero(string shortName)
    {
        var hero = await _dbContext.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == shortName.ToLowerInvariant());

        if (hero == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = "Hero not found",
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var dto = new HeroDetailDto
        {
            Id = hero.Id,
            ShortName = hero.ShortName,
            HyperlinkId = hero.HyperlinkId,
            AttributeId = hero.AttributeId,
            Name = hero.Name,
            Icon = hero.Icon,
            Role = hero.Role,
            ExpandedRole = hero.ExpandedRole,
            Type = hero.Type,
            ReleaseDate = hero.ReleaseDate,
            ReleasePatch = hero.ReleasePatch,
            Tags = hero.TagsJson != null
                ? JsonSerializer.Deserialize<List<string>>(hero.TagsJson) ?? new List<string>()
                : new List<string>()
        };

        // Group abilities by form
        dto.Abilities = hero.Abilities
            .GroupBy(a => a.FormName ?? hero.Name)
            .ToDictionary(
                g => g.Key,
                g => g.Select(a => new AbilityDto
                {
                    Uid = a.Uid,
                    Name = a.Name,
                    Description = a.Description,
                    Hotkey = a.Hotkey,
                    AbilityId = a.AbilityId,
                    Cooldown = a.Cooldown,
                    ManaCost = a.ManaCost,
                    Icon = a.Icon,
                    Type = a.Type,
                    IsTrait = a.IsTrait
                }).ToList()
            );

        // Group talents by level
        dto.Talents = hero.Talents
            .GroupBy(t => t.Level)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => t.Sort).Select(t => new TalentDto
                {
                    TooltipId = t.TooltipId,
                    TalentTreeId = t.TalentTreeId,
                    Name = t.Name,
                    Description = t.Description,
                    Icon = t.Icon,
                    Type = t.Type,
                    Sort = t.Sort,
                    Cooldown = t.Cooldown,
                    AbilityId = t.AbilityId,
                    AbilityLinks = t.AbilityLinksJson != null
                        ? JsonSerializer.Deserialize<List<string>>(t.AbilityLinksJson) ?? new List<string>()
                        : new List<string>()
                }).ToList()
            );

        return Ok(dto);
    }

    /// <summary>
    /// Get all distinct roles for filtering.
    /// </summary>
    [HttpGet("roles")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetRoles()
    {
        var roles = await _dbContext.Heroes
            .Where(h => h.ExpandedRole != null)
            .Select(h => h.ExpandedRole!)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        return Ok(roles);
    }

    /// <summary>
    /// Get all patches that affected a specific hero, ordered by date (most recent first).
    /// </summary>
    [HttpGet("{shortName}/patches")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<HeroPatchDto>>> GetHeroPatches(string shortName)
    {
        var hero = await _dbContext.Heroes
            .FirstOrDefaultAsync(h => h.ShortName == shortName.ToLowerInvariant());

        if (hero == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = "Hero not found",
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var sections = await _dbContext.PatchSections
            .Include(s => s.Patch)
            .Where(s => s.HeroId == hero.Id || 
                        s.EntityName.Equals(hero.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Patch.LiveDate)
            .Select(s => new HeroPatchDto
            {
                PatchId = s.PatchId,
                PatchName = s.Patch.PatchName,
                PatchType = s.Patch.PatchType,
                LiveDate = s.Patch.LiveDate,
                OfficialLink = s.Patch.OfficialLink,
                Content = s.Content,
                ContentHtml = s.ContentHtml,
                SectionType = s.SectionType
            })
            .ToListAsync();

        return Ok(sections);
    }
}
