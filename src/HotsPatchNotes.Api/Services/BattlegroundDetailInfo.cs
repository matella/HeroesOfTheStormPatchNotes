namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Detailed battleground info from individual wiki pages.
/// </summary>
public class BattlegroundDetailInfo
{
    public string? Description { get; set; }
    public string? ObjectiveDetails { get; set; }
    public string? ObjectiveTiming { get; set; }
    public string? MercCamps { get; set; }
    public string? BossInfo { get; set; }
    public string? Tips { get; set; }
    public string? FullImageUrl { get; set; }
}
