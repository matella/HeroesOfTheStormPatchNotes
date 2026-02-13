namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Basic battleground info from the main wiki table.
/// </summary>
public class BattlegroundBasicInfo
{
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string WikiUrl { get; set; } = string.Empty;
    public string ObjectiveSummary { get; set; } = string.Empty;
    public string Lanes { get; set; } = "3";
    public string Realm { get; set; } = string.Empty;
    public string Universe { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public string? ThumbnailUrl { get; set; }
}
