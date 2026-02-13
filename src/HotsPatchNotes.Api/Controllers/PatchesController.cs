using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PatchesController(IPatchService patchService) : ControllerBase
{
    /// <summary>
    /// Get all patches with optional filtering and pagination.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<PagedResultDto<PatchSummaryDto>>> GetPatchesAsync(
        [FromQuery] string? patchType = null,
        [FromQuery] string? source = null,
        [FromQuery] int page = Constants.Pagination.DefaultPage,
        [FromQuery] int pageSize = Constants.Pagination.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await patchService.GetPatchesAsync(patchType, source, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all distinct patch types for filtering.
    /// </summary>
    [HttpGet("types")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetPatchTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = await patchService.GetPatchTypesAsync(cancellationToken);
        return Ok(types);
    }

    /// <summary>
    /// Get all distinct data sources.
    /// </summary>
    [HttpGet("sources")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await patchService.GetSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    /// <summary>
    /// Get patch details with reconstructed content, navigation and sections.
    /// </summary>
    [HttpGet("{internalId}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<ReconstructedPatchDto>> GetPatchAsync(
        string internalId,
        CancellationToken cancellationToken = default)
    {
        var patch = await patchService.GetPatchAsync(internalId, cancellationToken);

        if (patch is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.PatchNotFound,
                Detail = $"No patch found with internal ID: {internalId}",
                StatusCode = 404
            });
        }

        return Ok(patch);
    }

    /// <summary>
    /// Get sections for a specific patch with optional filtering.
    /// </summary>
    [HttpGet("{internalId}/sections")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<PatchSectionDto>>> GetPatchSectionsAsync(
        string internalId,
        [FromQuery] string? sectionType = null,
        [FromQuery] string? entityName = null,
        CancellationToken cancellationToken = default)
    {
        var sections = await patchService.GetSectionsAsync(internalId, sectionType, entityName, cancellationToken);
        return Ok(sections);
    }
}
