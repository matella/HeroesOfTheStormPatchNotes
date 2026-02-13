using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HeroesController(IHeroService heroService) : ControllerBase
{
    /// <summary>
    /// Get all heroes with optional filtering.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<HeroSummaryDto>>> GetHeroesAsync(
        [FromQuery] string? role = null,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var heroes = await heroService.GetHeroesAsync(role, type, search, cancellationToken);
        return Ok(heroes);
    }

    /// <summary>
    /// Get a specific hero by short name with all abilities and talents.
    /// </summary>
    [HttpGet("{shortName}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<HeroDetailDto>> GetHeroAsync(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var hero = await heroService.GetHeroAsync(shortName, cancellationToken);

        if (hero is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.HeroNotFound,
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        return Ok(hero);
    }

    /// <summary>
    /// Get all distinct roles for filtering.
    /// </summary>
    [HttpGet("roles")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await heroService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Get all patches that affected a specific hero, ordered by date (most recent first).
    /// </summary>
    [HttpGet("{shortName}/patches")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<HeroPatchDto>>> GetHeroPatchesAsync(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var hero = await heroService.GetHeroAsync(shortName, cancellationToken);

        if (hero is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.HeroNotFound,
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var patches = await heroService.GetHeroPatchesAsync(shortName, cancellationToken);
        return Ok(patches);
    }

    /// <summary>
    /// Get all builds for a specific hero.
    /// </summary>
    [HttpGet("{shortName}/builds")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<List<HeroBuildDto>>> GetHeroBuildsAsync(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var hero = await heroService.GetHeroAsync(shortName, cancellationToken);

        if (hero is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.HeroNotFound,
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var builds = await heroService.GetHeroBuildsAsync(shortName, cancellationToken);
        return Ok(builds);
    }

    /// <summary>
    /// Create a new build for a hero.
    /// </summary>
    [HttpPost("{shortName}/builds")]
    public async Task<ActionResult<HeroBuildDto>> CreateBuildAsync(
        string shortName,
        [FromBody] CreateBuildDto request,
        CancellationToken cancellationToken = default)
    {
        var hero = await heroService.GetHeroAsync(shortName, cancellationToken);

        if (hero is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.HeroNotFound,
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var build = await heroService.CreateBuildAsync(shortName, request, cancellationToken);

        if (build is null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.InvalidTalentCode,
                Detail = $"Talent code must be {Constants.Talents.TierCount} digits, each between {Constants.Talents.MinChoice}-{Constants.Talents.MaxChoice}",
                StatusCode = 400
            });
        }

        return CreatedAtAction(nameof(GetHeroBuildsAsync), new { shortName }, build);
    }

    /// <summary>
    /// Parse a build code and return the talent selections.
    /// Format: [T1331221,heroname] or just "1331221"
    /// </summary>
    [HttpGet("builds/parse")]
    public ActionResult<object> ParseBuildCode([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.BuildCodeRequired,
                StatusCode = 400
            });
        }

        var result = heroService.ParseBuildCode(code);

        if (result is null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.InvalidBuildCodeFormat,
                Detail = $"Expected format: [T1234567,heroname] or {Constants.Talents.TierCount} digits",
                StatusCode = 400
            });
        }

        return Ok(new
        {
            result.TalentCode,
            result.HeroShortName,
            result.Talents
        });
    }
}
