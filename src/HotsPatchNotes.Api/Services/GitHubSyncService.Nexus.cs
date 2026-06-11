using System.Text.Json;
using System.Text.RegularExpressions;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Nexus Patch Notes source (https://nexus-patch-notes.github.io) — a community-maintained,
/// complete archive of HotS patch notes (2014 alpha → today) as one static HTML file per patch in
/// a public GitHub repo. No scraping of a dynamic site: we list the repo's patches/ directory via
/// the GitHub contents API and download raw files. Idempotent on InternalId ("nexus-&lt;file&gt;").
/// </summary>
public sealed partial class GitHubSyncService
{
    private const string NexusPatchesApiUrl =
        "https://api.github.com/repos/nexus-patch-notes/nexus-patch-notes.github.io/contents/patches";

    private sealed record NexusFileEntry(string Name, string DownloadUrl);

    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})-([a-z0-9-]+)\.html$", RegexOptions.IgnoreCase)]
    private static partial Regex NexusFileNameRegex();

    public async Task<SyncResultDto> SyncPatchesFromNexusAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };
        List<NexusFileEntry> files;
        try
        {
            var json = await httpClient.GetStringAsync(NexusPatchesApiUrl, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            files = doc.RootElement.EnumerateArray()
                .Select(e => new NexusFileEntry(
                    e.GetProperty("name").GetString() ?? "",
                    e.GetProperty("download_url").GetString() ?? ""))
                .Where(f => f.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nexus: failed to list patches directory");
            result.Errors.Add($"Nexus listing failed: {ex.Message}");
            return result;
        }

        var known = (await dbContext.Patches.Select(p => p.InternalId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = NexusFileNameRegex().Match(file.Name);
            if (!match.Success)
                continue;
            var internalId = "nexus-" + file.Name[..^".html".Length];
            if (known.Contains(internalId))
                continue;

            try
            {
                var html = await httpClient.GetStringAsync(file.DownloadUrl, cancellationToken);
                var patch = ParseNexusPatch(internalId, match, html);
                dbContext.Patches.Add(patch);
                added++;
                if (added % 25 == 0)
                    await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nexus: failed to import {File}", file.Name);
                result.Errors.Add($"Nexus {file.Name}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        result.Success = result.Errors.Count == 0;
        result.PatchesUpdated = added;
        result.Message = $"Nexus sync: {added} new patches imported ({files.Count} in archive)";
        logger.LogInformation("Nexus sync complete: {Added} new patches ({Total} in archive)",
            added, files.Count);
        return result;
    }

    private static Patch ParseNexusPatch(string internalId, Match nameMatch, string html)
    {
        var date = new DateTime(
            int.Parse(nameMatch.Groups[1].Value),
            int.Parse(nameMatch.Groups[2].Value),
            int.Parse(nameMatch.Groups[3].Value), 0, 0, 0, DateTimeKind.Utc);
        var kind = nameMatch.Groups[4].Value.ToLowerInvariant();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var main = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'main-content')]")
                   ?? doc.DocumentNode.SelectSingleNode("//body");
        var officialLink = doc.DocumentNode
            .SelectNodes("//a[contains(@href,'news.blizzard.com')]")
            ?.FirstOrDefault()?.GetAttributeValue("href", null);

        var patchType = kind switch
        {
            "live" => "Patch Notes",
            "ptr" => "PTR",
            "hotfix" => "Hotfix Patch",
            "revert" => "Hotfix Patch",
            "alpha" => "Alpha Patch",
            "beta" => "Beta Patch",
            _ => "Patch Notes",
        };

        return new Patch
        {
            InternalId = internalId,
            PatchName = $"{date:yyyy-MM-dd} {char.ToUpperInvariant(kind[0])}{kind[1..]}",
            PatchType = patchType,
            LiveDate = kind == "ptr" ? null : date,
            PtrDate = kind == "ptr" ? date : null,
            OfficialLink = officialLink,
            ContentHtml = main?.InnerHtml,
            Content = NormalizeWhitespace(main?.InnerText),
            Source = "nexus",
            LastSyncedAt = DateTime.UtcNow,
        };
    }

    private static string? NormalizeWhitespace(string? text) =>
        text is null ? null : Regex.Replace(HtmlEntity.DeEntitize(text), @"[ \t]*\n[ \t\n]*", "\n").Trim();
}
