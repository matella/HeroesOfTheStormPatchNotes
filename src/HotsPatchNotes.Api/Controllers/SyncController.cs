using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly IGitHubSyncService _syncService;
    private readonly ILogger<SyncController> _logger;

    public SyncController(IGitHubSyncService syncService, ILogger<SyncController> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>
    /// Trigger a full sync of all data from GitHub and web sources.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> SyncAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting full data sync...");
        var result = await _syncService.SyncAllAsync(cancellationToken);
        
        if (result.Success)
        {
            _logger.LogInformation("Sync completed successfully. Heroes: {Heroes}, Patches: {Patches}", 
                result.HeroesUpdated, result.PatchesUpdated);
            return Ok(result);
        }

        _logger.LogWarning("Sync completed with errors: {Errors}", string.Join(", ", result.Errors));
        return StatusCode(207, result); // 207 Multi-Status for partial success
    }

    /// <summary>
    /// Trigger a sync of hero data only.
    /// </summary>
    [HttpPost("heroes")]
    public async Task<ActionResult<SyncResultDto>> SyncHeroes(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting heroes sync...");
        var result = await _syncService.SyncHeroesAsync(cancellationToken);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(207, result);
    }

    /// <summary>
    /// Trigger a sync of patch data from GitHub only.
    /// </summary>
    [HttpPost("patches")]
    public async Task<ActionResult<SyncResultDto>> SyncPatches(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting patches sync from GitHub...");
        var result = await _syncService.SyncPatchesAsync(cancellationToken);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(207, result);
    }

    /// <summary>
    /// Trigger a sync of patch data from web sources (BlueTracker, Blizzard News).
    /// </summary>
    [HttpPost("patches/web")]
    public async Task<ActionResult<SyncResultDto>> SyncWebPatches(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting patches sync from web sources...");
        var result = await _syncService.SyncWebPatchesAsync(isInitialSync: false, cancellationToken);
        
        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(207, result);
    }
}
