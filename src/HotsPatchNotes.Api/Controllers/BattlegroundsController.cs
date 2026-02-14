using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BattlegroundsController(
    IBattlegroundService battlegroundService,
    ILogger<BattlegroundsController> logger) : ControllerBase
{
    /// <summary>
    /// Get all battlegrounds
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BattlegroundSummaryDto>>> GetBattlegrounds(
        [FromQuery] bool? inRotation = null,
        [FromQuery] string? universe = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var battlegrounds = await battlegroundService.GetBattlegroundsAsync(inRotation, universe, cancellationToken);
            return Ok(battlegrounds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve battlegrounds with filters - InRotation: {InRotation}, Universe: {Universe}", inRotation, universe);
            throw; // Let global middleware handle
        }
    }

    /// <summary>
    /// Get a specific battleground by short name
    /// </summary>
    [HttpGet("{shortName}")]
    public async Task<ActionResult<BattlegroundDetailDto>> GetBattleground(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var battleground = await battlegroundService.GetBattlegroundAsync(shortName, cancellationToken);

        if (battleground is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.BattlegroundNotFound,
                Detail = $"No battleground found with short name: {shortName}",
                StatusCode = 404
            });
        }

        return Ok(battleground);
    }

    /// <summary>
    /// Create a new battleground
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BattlegroundSummaryDto>> CreateBattleground(
        [FromBody] CreateBattlegroundDto dto,
        CancellationToken cancellationToken = default)
    {
        if (await battlegroundService.ExistsAsync(dto.ShortName, cancellationToken))
        {
            return Conflict(new ErrorResponseDto
            {
                Message = "Battleground already exists",
                Detail = $"A battleground with short name '{dto.ShortName}' already exists",
                StatusCode = 409
            });
        }

        var battleground = await battlegroundService.CreateBattlegroundAsync(dto, cancellationToken);

        if (battleground is null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = "Failed to create battleground",
                StatusCode = 400
            });
        }

        logger.LogInformation("Created battleground: {Name}", battleground.Name);

        return CreatedAtAction(
            nameof(GetBattleground),
            new { shortName = battleground.ShortName },
            battleground);
    }

    /// <summary>
    /// Update an existing battleground
    /// </summary>
    [HttpPut("{shortName}")]
    public async Task<ActionResult> UpdateBattleground(
        string shortName,
        [FromBody] CreateBattlegroundDto dto,
        CancellationToken cancellationToken = default)
    {
        var success = await battlegroundService.UpdateBattlegroundAsync(shortName, dto, cancellationToken);

        if (!success)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.BattlegroundNotFound,
                Detail = $"No battleground found with short name: {shortName}",
                StatusCode = 404
            });
        }

        logger.LogInformation("Updated battleground: {ShortName}", shortName);

        return NoContent();
    }

    /// <summary>
    /// Get patch history for a battleground
    /// </summary>
    [HttpGet("{shortName}/patches")]
    public async Task<ActionResult<List<BattlegroundPatchDto>>> GetBattlegroundPatches(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var battleground = await battlegroundService.GetBattlegroundAsync(shortName, cancellationToken);

        if (battleground is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.BattlegroundNotFound,
                Detail = $"No battleground found with short name: {shortName}",
                StatusCode = 404
            });
        }

        var patches = await battlegroundService.GetBattlegroundPatchesAsync(shortName, cancellationToken);
        return Ok(patches);
    }
}
