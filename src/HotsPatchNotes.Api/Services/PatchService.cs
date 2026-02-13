using System.Text;
using System.Text.RegularExpressions;
using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service implementation for Patch business operations.
/// </summary>
public sealed partial class PatchService(IPatchRepository patchRepository) : IPatchService
{
    public async Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(
        string? patchType = null,
        string? source = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (items, totalCount) = await patchRepository.GetAllAsync(patchType, source, page, pageSize, cancellationToken);

        var patches = items.Select(p => new PatchSummaryDto
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
        }).ToList();

        return new PagedResultDto<PatchSummaryDto>
        {
            Items = patches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReconstructedPatchDto?> GetPatchAsync(string internalId, CancellationToken cancellationToken = default)
    {
        var patch = await patchRepository.GetByInternalIdAsync(internalId, cancellationToken);
        if (patch is null)
        {
            return null;
        }

        var toc = patch.Sections
            .Select(s => new PatchTocItemDto
            {
                Order = s.Order,
                HeadingLevel = s.HeadingLevel,
                Title = s.EntityName,
                Anchor = GenerateAnchor(s.EntityName, s.Order),
                SectionType = s.SectionType
            })
            .ToList();

        var sections = patch.Sections
            .Select(s => new PatchSectionDto
            {
                Id = s.Id,
                Order = s.Order,
                HeadingLevel = s.HeadingLevel,
                SectionType = s.SectionType,
                EntityName = s.EntityName,
                HeroId = s.HeroId,
                HeroShortName = s.Hero?.ShortName,
                Content = s.Content
            })
            .ToList();

        var reconstructedContent = ReconstructMarkdown(patch.Sections.ToList());

        return new ReconstructedPatchDto
        {
            Id = patch.Id,
            InternalId = patch.InternalId,
            PatchName = patch.PatchName,
            PatchType = patch.PatchType,
            LiveDate = patch.LiveDate,
            OfficialLink = patch.OfficialLink,
            Content = reconstructedContent,
            TableOfContents = toc,
            Sections = sections
        };
    }

    public async Task<List<string>> GetPatchTypesAsync(CancellationToken cancellationToken = default)
    {
        return await patchRepository.GetPatchTypesAsync(cancellationToken);
    }

    public async Task<List<string>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await patchRepository.GetSourcesAsync(cancellationToken);
    }

    public async Task<List<PatchSectionDto>> GetSectionsAsync(
        string internalId,
        string? sectionType = null,
        string? entityName = null,
        CancellationToken cancellationToken = default)
    {
        var sections = await patchRepository.GetSectionsAsync(internalId, sectionType, entityName, cancellationToken);

        return sections.Select(s => new PatchSectionDto
        {
            Id = s.Id,
            Order = s.Order,
            HeadingLevel = s.HeadingLevel,
            SectionType = s.SectionType,
            EntityName = s.EntityName,
            HeroId = s.HeroId,
            HeroShortName = s.Hero?.ShortName,
            Content = s.Content
        }).ToList();
    }

    private static string GenerateAnchor(string title, int order)
    {
        var anchor = title.ToLowerInvariant();
        anchor = NonAlphanumericRegex().Replace(anchor, string.Empty);
        anchor = WhitespaceRegex().Replace(anchor, "-");
        anchor = MultipleDashRegex().Replace(anchor, "-");
        anchor = anchor.Trim('-');

        return $"{anchor}-{order}";
    }

    private static string ReconstructMarkdown(List<PatchSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<a id=\"return\"></a>");
        sb.AppendLine();

        foreach (var section in sections.OrderBy(s => s.Order))
        {
            var heading = new string('#', section.HeadingLevel);
            var anchor = GenerateAnchor(section.EntityName, section.Order);
            sb.AppendLine($"{heading} {section.EntityName} {{#{anchor}}}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(section.Content))
            {
                sb.AppendLine(section.Content);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashRegex();
}
