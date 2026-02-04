using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.DTOs;
using HotsPatchNotes.Shared.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public partial class PatchesController : ControllerBase
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
    public async Task<ActionResult<PagedResultDto<PatchSummaryDto>>> GetPatchesAsync(
        [FromQuery] string? patchType = null,
        [FromQuery] string? source = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
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

        var totalCount = await query.CountAsync(cancellationToken);

        var patches = await query
            .OrderByDescending(p => p.LiveDate ?? DateTime.MinValue)
            .ThenByDescending(p => p.Id)
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
            .ToListAsync(cancellationToken);

        return Ok(new PagedResultDto<PatchSummaryDto>
        {
            Items = patches,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get all distinct patch types for filtering.
    /// </summary>
    [HttpGet("types")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetPatchTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = await _dbContext.Patches
            .Where(p => p.PatchType != null)
            .Select(p => p.PatchType!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);

        return Ok(types);
    }

    /// <summary>
    /// Get all distinct data sources.
    /// </summary>
    [HttpGet("sources")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<List<string>>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await _dbContext.Patches
            .Where(p => p.Source != null)
            .Select(p => p.Source!)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(cancellationToken);

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
        var patch = await _dbContext.Patches
            .Include(p => p.Sections.OrderBy(s => s.Order))
            .ThenInclude(s => s.Hero)
            .FirstOrDefaultAsync(p => p.InternalId == internalId, cancellationToken);

        if (patch is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = "Patch not found",
                Detail = $"No patch found with internal ID: {internalId}",
                StatusCode = 404
            });
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

        return Ok(new ReconstructedPatchDto
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
        });
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
        var query = _dbContext.PatchSections
            .Include(s => s.Hero)
            .Include(s => s.Patch)
            .Where(s => s.Patch.InternalId == internalId);

        if (!string.IsNullOrWhiteSpace(sectionType))
        {
            query = query.Where(s => s.SectionType == sectionType);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(s => s.EntityName.Contains(entityName));
        }

        var sections = await query
            .OrderBy(s => s.Order)
            .Select(s => new PatchSectionDto
            {
                Id = s.Id,
                Order = s.Order,
                HeadingLevel = s.HeadingLevel,
                SectionType = s.SectionType,
                EntityName = s.EntityName,
                HeroId = s.HeroId,
                HeroShortName = s.Hero != null ? s.Hero.ShortName : null,
                Content = s.Content
            })
            .ToListAsync(cancellationToken);

        return Ok(sections);
    }

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashRegex();

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
}
