using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SyncController(IGitHubSyncService syncService, ILogger<SyncController> logger) : ControllerBase
{

    /// <summary>
    /// Trigger a full sync of all data from GitHub and web sources.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResultDto>> SyncAllAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting full data sync...");
        var result = await syncService.SyncAllAsync(cancellationToken);

        if (result.Success)
        {
            logger.LogInformation(
                "Sync completed successfully. Heroes: {Heroes}, Patches: {Patches}",
                result.HeroesUpdated,
                result.PatchesUpdated);
            return Ok(result);
        }

        logger.LogWarning("Sync completed with errors: {Errors}", string.Join(", ", result.Errors));
        return StatusCode(207, result);
    }

    /// <summary>
    /// Trigger a sync of hero data only.
    /// </summary>
    [HttpPost("heroes")]
    public async Task<ActionResult<SyncResultDto>> SyncHeroesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting heroes sync...");
        var result = await syncService.SyncHeroesAsync(cancellationToken);

        return result.Success ? Ok(result) : StatusCode(207, result);
    }

    /// <summary>
    /// Trigger a sync of patch data from GitHub archive repository only.
    /// </summary>
    [HttpPost("patches/github")]
    public async Task<ActionResult<SyncResultDto>> SyncPatchesFromGitHubAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting patches sync from GitHub archive...");
        var result = await syncService.SyncPatchesFromGitHubAsync(cancellationToken);

        return result.Success ? Ok(result) : StatusCode(207, result);
    }

    /// <summary>
    /// Trigger a sync of patch data from BlueTracker web scraping.
    /// </summary>
    [HttpPost("patches/bluetracker")]
    public async Task<ActionResult<SyncResultDto>> SyncPatchesFromBlueTrackerAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting patches sync from BlueTracker...");
        var result = await syncService.SyncPatchesFromBlueTrackerAsync(isInitialSync: false, cancellationToken);

        return result.Success ? Ok(result) : StatusCode(207, result);
    }
}
