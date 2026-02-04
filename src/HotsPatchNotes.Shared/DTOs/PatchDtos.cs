namespace HotsPatchNotes.Shared.DTOs;

/// <summary>
/// Summary DTO for patch list views.
/// </summary>
public class PatchSummaryDto
{
    public int Id { get; set; }
    public string InternalId { get; set; } = string.Empty;
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public string? GameVersion { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? OfficialLink { get; set; }
    public string? AlternateLink { get; set; }
    public string? Source { get; set; }
    public bool HasContent { get; set; }
}

/// <summary>
/// DTO for hero-specific patch changes.
/// </summary>
public class HeroPatchDto
{
    public int PatchId { get; set; }
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? OfficialLink { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
    public int HeadingLevel { get; set; }
    public int Order { get; set; }
}

/// <summary>
/// DTO for patch section in detail views.
/// </summary>
public class PatchSectionDto
{
    public int Id { get; set; }
    public int Order { get; set; }
    public int HeadingLevel { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? HeroId { get; set; }
    public string? HeroShortName { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// DTO for patch detail with reconstructed content, navigation and sections.
/// </summary>
public class ReconstructedPatchDto
{
    public int Id { get; set; }
    public string InternalId { get; set; } = string.Empty;
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? OfficialLink { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<PatchTocItemDto> TableOfContents { get; set; } = [];
    public List<PatchSectionDto> Sections { get; set; } = [];
}

/// <summary>
/// DTO for table of contents item.
/// </summary>
public class PatchTocItemDto
{
    public int Order { get; set; }
    public int HeadingLevel { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Anchor { get; set; } = string.Empty;
    public string SectionType { get; set; } = string.Empty;
}
