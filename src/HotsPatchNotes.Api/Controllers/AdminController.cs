using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AdminController(HotsDbContext dbContext, ILogger<AdminController> logger) : ControllerBase
{
    // --- Heroes ---

    [HttpGet("heroes")]
    public async Task<ActionResult<PagedResult<AdminHeroDto>>> GetHeroesAsync(
        [FromQuery] int page = Constants.Pagination.DefaultPage,
        [FromQuery] int pageSize = Constants.Pagination.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Heroes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(h => h.Name.Contains(search) || h.ShortName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var heroes = await query
            .OrderBy(h => h.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new AdminHeroDto
            {
                Id = h.Id,
                ShortName = h.ShortName,
                Name = h.Name,
                Icon = h.Icon,
                Role = h.Role,
                ExpandedRole = h.ExpandedRole,
                Type = h.Type,
                ReleaseDate = h.ReleaseDate,
                Title = h.Title,
                Universe = h.Universe,
                Difficulty = h.Difficulty,
                Description = h.Description,
                Lore = h.Lore,
                WikiUrl = h.WikiUrl,
                SplashArtUrl = h.SplashArtUrl,
                BaseHealth = h.BaseHealth,
                HealthRegen = h.HealthRegen,
                BaseMana = h.BaseMana,
                ManaRegen = h.ManaRegen,
                BaseAttackDamage = h.BaseAttackDamage,
                AttackSpeed = h.AttackSpeed,
                AttackRange = h.AttackRange,
                AbilityCount = h.Abilities.Count,
                TalentCount = h.Talents.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AdminHeroDto>
        {
            Items = heroes,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpPut("heroes/{id:int}")]
    public async Task<ActionResult<AdminHeroDto>> UpdateHeroAsync(
        int id,
        [FromBody] AdminHeroDto dto,
        CancellationToken cancellationToken = default)
    {
        var hero = await dbContext.Heroes.FindAsync([id], cancellationToken);
        if (hero is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.HeroNotFound, StatusCode = 404 });
        }

        hero.Name = dto.Name;
        hero.ShortName = dto.ShortName;
        hero.Icon = dto.Icon;
        hero.Role = dto.Role;
        hero.ExpandedRole = dto.ExpandedRole;
        hero.Type = dto.Type;
        hero.ReleaseDate = dto.ReleaseDate;
        hero.Title = dto.Title;
        hero.Universe = dto.Universe;
        hero.Difficulty = dto.Difficulty;
        hero.Description = dto.Description;
        hero.Lore = dto.Lore;
        hero.WikiUrl = dto.WikiUrl;
        hero.SplashArtUrl = dto.SplashArtUrl;
        hero.BaseHealth = dto.BaseHealth;
        hero.HealthRegen = dto.HealthRegen;
        hero.BaseMana = dto.BaseMana;
        hero.ManaRegen = dto.ManaRegen;
        hero.BaseAttackDamage = dto.BaseAttackDamage;
        hero.AttackSpeed = dto.AttackSpeed;
        hero.AttackRange = dto.AttackRange;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin updated hero {HeroId}: {HeroName}", id, hero.Name);

        return Ok(dto);
    }

    [HttpDelete("heroes/{id:int}")]
    public async Task<ActionResult> DeleteHeroAsync(int id, CancellationToken cancellationToken = default)
    {
        var hero = await dbContext.Heroes.FindAsync([id], cancellationToken);
        if (hero is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.HeroNotFound, StatusCode = 404 });
        }

        dbContext.Heroes.Remove(hero);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin deleted hero {HeroId}: {HeroName}", id, hero.Name);

        return NoContent();
    }

    // --- Abilities ---

    [HttpGet("heroes/{heroId:int}/abilities")]
    public async Task<ActionResult<List<AdminAbilityDto>>> GetAbilitiesAsync(
        int heroId,
        CancellationToken cancellationToken = default)
    {
        var abilities = await dbContext.Abilities
            .Where(a => a.HeroId == heroId)
            .OrderBy(a => a.FormName).ThenBy(a => a.Type).ThenBy(a => a.Hotkey)
            .Select(a => new AdminAbilityDto
            {
                Id = a.Id,
                HeroId = a.HeroId,
                Name = a.Name,
                Description = a.Description,
                Hotkey = a.Hotkey,
                Cooldown = a.Cooldown,
                ManaCost = a.ManaCost,
                Icon = a.Icon,
                Type = a.Type,
                FormName = a.FormName,
                Scaling = a.Scaling,
                CastTime = a.CastTime,
                Range = a.Range,
                AreaOfEffect = a.AreaOfEffect
            })
            .ToListAsync(cancellationToken);

        return Ok(abilities);
    }

    [HttpPut("abilities/{id:int}")]
    public async Task<ActionResult<AdminAbilityDto>> UpdateAbilityAsync(
        int id,
        [FromBody] AdminAbilityDto dto,
        CancellationToken cancellationToken = default)
    {
        var ability = await dbContext.Abilities.FindAsync([id], cancellationToken);
        if (ability is null)
        {
            return NotFound(new ErrorResponseDto { Message = "Ability not found", StatusCode = 404 });
        }

        ability.Name = dto.Name;
        ability.Description = dto.Description;
        ability.Hotkey = dto.Hotkey;
        ability.Cooldown = dto.Cooldown;
        ability.ManaCost = dto.ManaCost;
        ability.Icon = dto.Icon;
        ability.Type = dto.Type;
        ability.FormName = dto.FormName;
        ability.Scaling = dto.Scaling;
        ability.CastTime = dto.CastTime;
        ability.Range = dto.Range;
        ability.AreaOfEffect = dto.AreaOfEffect;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin updated ability {AbilityId}: {AbilityName}", id, ability.Name);

        return Ok(dto);
    }

    // --- Talents ---

    [HttpGet("heroes/{heroId:int}/talents")]
    public async Task<ActionResult<List<AdminTalentDto>>> GetTalentsAsync(
        int heroId,
        CancellationToken cancellationToken = default)
    {
        var talents = await dbContext.Talents
            .Where(t => t.HeroId == heroId)
            .OrderBy(t => t.Level).ThenBy(t => t.Sort)
            .Select(t => new AdminTalentDto
            {
                Id = t.Id,
                HeroId = t.HeroId,
                Level = t.Level,
                Name = t.Name,
                Description = t.Description,
                Icon = t.Icon,
                Type = t.Type,
                Sort = t.Sort,
                Cooldown = t.Cooldown,
                LinkedAbilityName = t.LinkedAbilityName,
                Properties = t.Properties
            })
            .ToListAsync(cancellationToken);

        return Ok(talents);
    }

    [HttpPut("talents/{id:int}")]
    public async Task<ActionResult<AdminTalentDto>> UpdateTalentAsync(
        int id,
        [FromBody] AdminTalentDto dto,
        CancellationToken cancellationToken = default)
    {
        var talent = await dbContext.Talents.FindAsync([id], cancellationToken);
        if (talent is null)
        {
            return NotFound(new ErrorResponseDto { Message = "Talent not found", StatusCode = 404 });
        }

        talent.Name = dto.Name;
        talent.Description = dto.Description;
        talent.Icon = dto.Icon;
        talent.Type = dto.Type;
        talent.Sort = dto.Sort;
        talent.Cooldown = dto.Cooldown;
        talent.LinkedAbilityName = dto.LinkedAbilityName;
        talent.Properties = dto.Properties;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin updated talent {TalentId}: {TalentName}", id, talent.Name);

        return Ok(dto);
    }

    // --- Patches ---

    [HttpGet("patches")]
    public async Task<ActionResult<PagedResult<AdminPatchDto>>> GetPatchesAsync(
        [FromQuery] int page = Constants.Pagination.DefaultPage,
        [FromQuery] int pageSize = Constants.Pagination.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Patches.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.PatchName != null && p.PatchName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var patches = await query
            .OrderByDescending(p => p.LiveDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminPatchDto
            {
                Id = p.Id,
                InternalId = p.InternalId,
                PatchName = p.PatchName,
                PatchType = p.PatchType,
                GameVersion = p.GameVersion,
                LiveDate = p.LiveDate,
                OfficialLink = p.OfficialLink,
                AlternateLink = p.AlternateLink,
                Source = p.Source,
                HasContent = p.Content != null,
                SectionCount = p.Sections.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<AdminPatchDto>
        {
            Items = patches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpPut("patches/{id:int}")]
    public async Task<ActionResult<AdminPatchDto>> UpdatePatchAsync(
        int id,
        [FromBody] AdminPatchDto dto,
        CancellationToken cancellationToken = default)
    {
        var patch = await dbContext.Patches.FindAsync([id], cancellationToken);
        if (patch is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.PatchNotFound, StatusCode = 404 });
        }

        patch.PatchName = dto.PatchName;
        patch.PatchType = dto.PatchType;
        patch.GameVersion = dto.GameVersion;
        patch.LiveDate = dto.LiveDate;
        patch.OfficialLink = dto.OfficialLink;
        patch.AlternateLink = dto.AlternateLink;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin updated patch {PatchId}: {PatchName}", id, patch.PatchName);

        return Ok(dto);
    }

    [HttpDelete("patches/{id:int}")]
    public async Task<ActionResult> DeletePatchAsync(int id, CancellationToken cancellationToken = default)
    {
        var patch = await dbContext.Patches.FindAsync([id], cancellationToken);
        if (patch is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.PatchNotFound, StatusCode = 404 });
        }

        dbContext.Patches.Remove(patch);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin deleted patch {PatchId}: {PatchName}", id, patch.PatchName);

        return NoContent();
    }

    // --- Battlegrounds ---

    [HttpGet("battlegrounds")]
    public async Task<ActionResult<List<AdminBattlegroundDto>>> GetBattlegroundsAsync(
        CancellationToken cancellationToken = default)
    {
        var battlegrounds = await dbContext.Battlegrounds
            .OrderBy(b => b.Name)
            .Select(b => new AdminBattlegroundDto
            {
                Id = b.Id,
                ShortName = b.ShortName,
                Name = b.Name,
                MapType = b.MapType,
                Description = b.Description,
                Objective = b.Objective,
                ObjectiveTiming = b.ObjectiveTiming,
                MercCamps = b.MercCamps,
                BossInfo = b.BossInfo,
                Tips = b.Tips,
                ImageUrl = b.ImageUrl,
                Universe = b.Universe,
                ReleaseDate = b.ReleaseDate,
                IsInRotation = b.IsInRotation
            })
            .ToListAsync(cancellationToken);

        return Ok(battlegrounds);
    }

    [HttpPut("battlegrounds/{id:int}")]
    public async Task<ActionResult<AdminBattlegroundDto>> UpdateBattlegroundAsync(
        int id,
        [FromBody] AdminBattlegroundDto dto,
        CancellationToken cancellationToken = default)
    {
        var battleground = await dbContext.Battlegrounds.FindAsync([id], cancellationToken);
        if (battleground is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.BattlegroundNotFound, StatusCode = 404 });
        }

        battleground.Name = dto.Name;
        battleground.ShortName = dto.ShortName;
        battleground.MapType = dto.MapType;
        battleground.Description = dto.Description;
        battleground.Objective = dto.Objective;
        battleground.ObjectiveTiming = dto.ObjectiveTiming;
        battleground.MercCamps = dto.MercCamps;
        battleground.BossInfo = dto.BossInfo;
        battleground.Tips = dto.Tips;
        battleground.ImageUrl = dto.ImageUrl;
        battleground.Universe = dto.Universe;
        battleground.ReleaseDate = dto.ReleaseDate;
        battleground.IsInRotation = dto.IsInRotation;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin updated battleground {BgId}: {BgName}", id, battleground.Name);

        return Ok(dto);
    }

    [HttpDelete("battlegrounds/{id:int}")]
    public async Task<ActionResult> DeleteBattlegroundAsync(int id, CancellationToken cancellationToken = default)
    {
        var battleground = await dbContext.Battlegrounds.FindAsync([id], cancellationToken);
        if (battleground is null)
        {
            return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.BattlegroundNotFound, StatusCode = 404 });
        }

        dbContext.Battlegrounds.Remove(battleground);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin deleted battleground {BgId}: {BgName}", id, battleground.Name);

        return NoContent();
    }
}
