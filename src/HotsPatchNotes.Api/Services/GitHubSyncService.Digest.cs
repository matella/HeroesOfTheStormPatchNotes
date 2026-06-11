using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Patch digest push (Nexus Codex phase 5): after each sync, POST a condensed "latest patch" JSON
/// (~2KB: classified heroes + maps with short summaries) to the stream-overlay instances —
/// the local one and the Azure-hosted one (which can't reach this box). Configured via
/// DIGEST_PUSH_URLS (comma-separated) + DIGEST_PUSH_TOKEN (the overlay's AUTH_TOKEN). No-op when
/// unconfigured; failures are logged and never break the sync.
/// </summary>
public sealed partial class GitHubSyncService
{
    public async Task PushPatchDigestAsync(CancellationToken cancellationToken = default)
    {
        var urls = (Environment.GetEnvironmentVariable("DIGEST_PUSH_URLS") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (urls.Length == 0)
            return;

        var patch = await dbContext.Patches
            .Where(p => p.LiveDate != null)
            .OrderByDescending(p => p.LiveDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (patch is null)
            return;

        var sections = await dbContext.PatchSections
            .Where(s => s.PatchId == patch.Id && s.SectionType != "General")
            .OrderBy(s => s.Order)
            .Select(s => new { s.SectionType, s.EntityName, s.Classification, s.ShortSummary })
            .ToListAsync(cancellationToken);

        var digest = new
        {
            patchName = patch.PatchName ?? patch.InternalId,
            patchType = patch.PatchType,
            liveDate = patch.LiveDate?.ToString("yyyy-MM-dd"),
            officialLink = patch.OfficialLink,
            heroes = sections.Where(s => s.SectionType == "Hero")
                .Select(s => new { name = s.EntityName, classification = s.Classification,
                                   summary = s.ShortSummary }),
            maps = sections.Where(s => s.SectionType == "Map")
                .Select(s => new { name = s.EntityName, classification = s.Classification,
                                   summary = s.ShortSummary }),
        };
        var json = JsonSerializer.Serialize(digest);
        var token = Environment.GetEnvironmentVariable("DIGEST_PUSH_TOKEN");

        foreach (var url in urls)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                if (!string.IsNullOrEmpty(token))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await httpClient.SendAsync(req, cancellationToken);
                logger.LogInformation("Patch digest push → {Url}: {Status}", url, resp.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Patch digest push failed for {Url}", url);
            }
        }
    }
}
