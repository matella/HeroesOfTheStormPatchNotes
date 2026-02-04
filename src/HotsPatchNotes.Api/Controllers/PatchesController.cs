using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatchesController : ControllerBase
{
    private readonly HotsDbContext _dbContext;

    public PatchesController(HotsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Get all patches with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<PagedResultDto<PatchSummaryDto>>> GetPatches(
        [FromQuery] string? patchType = null,
        [FromQuery] string? source = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _dbContext.Patches.AsQueryable();

        if (!string.IsNullOrWhiteSpace(patchType))
        {
            query = query.Where(p => p.PatchType == patchType);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(p => p.Source == source);
        }

        var totalCount = await query.CountAsync();

        var patches = await query
            .OrderByDescending(p => p.LiveDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PatchSummaryDto
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
                HasContent = p.Content != null
            })
            .ToListAsync();

        return Ok(new PagedResultDto<PatchSummaryDto>
        {
            Items = patches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get a specific patch by internal ID.
    /// </summary>
    [HttpGet("{internalId}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<PatchDetailDto>> GetPatch(string internalId)
    {
        var patch = await _dbContext.Patches
            .FirstOrDefaultAsync(p => p.InternalId == internalId);

        if (patch == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = "Patch not found",
                Detail = $"No patch found with internal ID: {internalId}",
                StatusCode = 404
            });
        }

        return Ok(new PatchDetailDto
        {
            Id = patch.Id,
            InternalId = patch.InternalId,
            PatchName = patch.PatchName,
            PatchType = patch.PatchType,
            GameVersion = patch.GameVersion,
            FullVersion = patch.FullVersion,
            OfficialLink = patch.OfficialLink,
            AlternateLink = patch.AlternateLink,
            LiveDate = patch.LiveDate,
            LiveBuild = patch.LiveBuild,
            PtrOfficialLink = patch.PtrOfficialLink,
            PtrDate = patch.PtrDate,
            PtrBuild = patch.PtrBuild,
            Content = patch.Content,
            ContentHtml = patch.ContentHtml,
            Source = patch.Source
        });
    }

    /// <summary>
    /// Get all distinct patch types for filtering.
    /// </summary>
    [HttpGet("types")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetPatchTypes()
    {
        var types = await _dbContext.Patches
            .Where(p => p.PatchType != null)
            .Select(p => p.PatchType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        return Ok(types);
    }

    /// <summary>
    /// Get all distinct data sources.
    /// </summary>
    [HttpGet("sources")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetSources()
    {
        var sources = await _dbContext.Patches
            .Where(p => p.Source != null)
            .Select(p => p.Source!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        return Ok(sources);
    }
}
