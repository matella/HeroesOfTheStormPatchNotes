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
                foreach (var section in ParseNexusSections(html, await HeroIdLookupAsync(cancellationToken)))
                    patch.Sections.Add(section);
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


    // ── Sections par héros / carte (le pont patches↔héros du Codex) ──────────────────────────

    private Dictionary<string, int>? _heroIdCache;

    private async Task<Dictionary<string, int>> HeroIdLookupAsync(CancellationToken ct)
    {
        return _heroIdCache ??= (await dbContext.Heroes
                .Select(h => new { h.Id, h.Name })
                .ToListAsync(ct))
            .GroupBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract per-hero / per-map / general sections from a Nexus patch page. Each
    /// div.section-block carries a label (icon + h2 name) and a div.section-html body.
    /// </summary>
    internal static List<PatchSection> ParseNexusSections(string html, Dictionary<string, int> heroIds)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var blocks = doc.DocumentNode.SelectNodes("//div[contains(@class,'section-block')]");
        var sections = new List<PatchSection>();
        if (blocks is null)
            return sections;

        var order = 0;
        foreach (var block in blocks)
        {
            var cls = block.GetAttributeValue("class", "");
            var type = cls.Contains("heroes-section") ? "Hero"
                     : cls.Contains("battleground") ? "Map"
                     : "General";
            var name = HtmlEntity.DeEntitize(
                block.SelectSingleNode(".//h2")?.InnerText
                ?? block.SelectSingleNode(".//img")?.GetAttributeValue("alt", "") ?? "").Trim();
            var body = block.SelectSingleNode(".//div[contains(@class,'section-html')]");
            var content = body?.InnerHtml?.Trim() ?? "";
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(content))
                continue;

            var verdict = PatchClassifier.Classify(
                HtmlEntity.DeEntitize(body?.InnerText ?? ""));
            sections.Add(new PatchSection
            {
                Order = order++,
                HeadingLevel = 2,
                SectionType = type,
                EntityName = name,
                HeroId = type == "Hero" && heroIds.TryGetValue(name, out var id) ? id : null,
                Content = content,
                Classification = verdict.Classification,
                ShortSummary = verdict.ShortSummary,
            });
        }
        return sections;
    }

    /// <summary>
    /// One-shot/idempotent: build sections for already-imported Nexus patches that have none
    /// (the 310-patch backfill). ContentHtml was stored at import, so no re-download is needed.
    /// </summary>
    public async Task<int> BackfillNexusSectionsAsync(CancellationToken cancellationToken = default)
    {
        var lookup = await HeroIdLookupAsync(cancellationToken);
        var patches = await dbContext.Patches
            .Where(p => p.Source == "nexus" && !p.Sections.Any() && p.ContentHtml != null)
            .ToListAsync(cancellationToken);
        var done = 0;
        foreach (var patch in patches)
        {
            foreach (var section in ParseNexusSections(patch.ContentHtml!, lookup))
            {
                section.PatchId = patch.Id;
                dbContext.PatchSections.Add(section);
            }
            done++;
            if (done % 25 == 0)
                await dbContext.SaveChangesAsync(cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Nexus backfill: sections built for {Count} patches", done);
        return done;
    }

    // ── Images héros + battlegrounds depuis le repo Nexus ────────────────────────────────────

    private const string NexusImagesApi =
        "https://api.github.com/repos/nexus-patch-notes/nexus-patch-notes.github.io/contents/images";

    public async Task<int> SyncNexusImagesAsync(CancellationToken cancellationToken = default)
    {
        var updated = 0;
        updated += await SyncImageCategoryAsync("heroes", cancellationToken);
        updated += await SyncImageCategoryAsync("battlegrounds", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Nexus images: {Count} entity images mapped", updated);
        return updated;
    }

    private async Task<int> SyncImageCategoryAsync(string category, CancellationToken ct)
    {
        List<NexusFileEntry> files;
        try
        {
            var json = await httpClient.GetStringAsync($"{NexusImagesApi}/{category}", ct);
            using var doc = JsonDocument.Parse(json);
            files = doc.RootElement.EnumerateArray()
                .Select(e => new NexusFileEntry(
                    e.GetProperty("name").GetString() ?? "",
                    e.GetProperty("download_url").GetString() ?? ""))
                .Where(f => f.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nexus images: listing {Category} failed", category);
            return 0;
        }

        var updated = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var slug = file.Name[..^".png".Length];
            var local = await imageDownloadService.DownloadImageAsync(file.DownloadUrl, category, slug, ct);
            if (local is null)
                continue;

            if (category == "heroes")
            {
                // Normalize both sides (nexus: thelostvikings / cho+gall; DB: the-lost-vikings /
                // chogall). cho.png is used for the merged Cho'gall hero.
                var norm = slug.Replace("-", "");
                if (norm == "cho") norm = "chogall";
                var heroes = await dbContext.Heroes.ToListAsync(ct);
                var hero = heroes.FirstOrDefault(h =>
                    h.ShortName.Replace("-", "").Equals(norm, StringComparison.OrdinalIgnoreCase)
                    || Slugify(h.Name) == slug);
                if (hero is not null && string.IsNullOrEmpty(hero.Icon))
                {
                    hero.Icon = local;
                    updated++;
                }
            }
            else
            {
                var normalized = slug.Replace("-", "");
                var bg = await dbContext.Battlegrounds.FirstOrDefaultAsync(
                    b => b.ShortName == slug || b.ShortName == normalized, ct);
                bg ??= (await dbContext.Battlegrounds.ToListAsync(ct)).FirstOrDefault(
                    b => Slugify(b.Name) == slug);
                if (bg is not null && string.IsNullOrEmpty(bg.ImageUrl))
                {
                    bg.ImageUrl = local;
                    updated++;
                }
            }
        }
        return updated;
    }

    private static string Slugify(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace("'", ""), @"[^a-z0-9]+", "-").Trim('-');

    private static string? NormalizeWhitespace(string? text) =>
        text is null ? null : Regex.Replace(HtmlEntity.DeEntitize(text), @"[ \t]*\n[ \t\n]*", "\n").Trim();
}
