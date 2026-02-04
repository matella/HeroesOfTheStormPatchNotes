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
/// Detailed DTO for patch detail views.
/// </summary>
public class PatchDetailDto
{
    public int Id { get; set; }
    public string InternalId { get; set; } = string.Empty;
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public string? GameVersion { get; set; }
    public string? FullVersion { get; set; }
    public string? OfficialLink { get; set; }
    public string? AlternateLink { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? LiveBuild { get; set; }
    public string? PtrOfficialLink { get; set; }
    public DateTime? PtrDate { get; set; }
    public string? PtrBuild { get; set; }
    public string? Content { get; set; }
    public string? ContentHtml { get; set; }
    public string? Source { get; set; }
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
    public string? ContentHtml { get; set; }
    public string SectionType { get; set; } = string.Empty;
}
