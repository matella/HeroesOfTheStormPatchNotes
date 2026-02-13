namespace HotsPatchNotes.Shared.DTOs;

/// <summary>
/// Summary DTO for battleground list views.
/// </summary>
public class BattlegroundSummaryDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MapType { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsInRotation { get; set; }
    public string? Universe { get; set; }
}

/// <summary>
/// Detailed DTO for battleground views.
/// </summary>
public class BattlegroundDetailDto
{
    public int Id { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MapType { get; set; }
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public string? ObjectiveTiming { get; set; }
    public string? MercCamps { get; set; }
    public string? BossInfo { get; set; }
    public string? Tips { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsInRotation { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public string? Event { get; set; }
    public string? Universe { get; set; }
    public List<BattlegroundPatchDto> RecentPatches { get; set; } = [];
}

/// <summary>
/// DTO for battleground patch history.
/// </summary>
public class BattlegroundPatchDto
{
    public int PatchId { get; set; }
    public string? PatchName { get; set; }
    public DateTime? LiveDate { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating/updating a battleground.
/// </summary>
public class CreateBattlegroundDto
{
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? MapType { get; set; }
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public string? ObjectiveTiming { get; set; }
    public string? MercCamps { get; set; }
    public string? BossInfo { get; set; }
    public string? Tips { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsInRotation { get; set; } = true;
    public DateTime? ReleaseDate { get; set; }
    public string? Event { get; set; }
    public string? Universe { get; set; }
}
