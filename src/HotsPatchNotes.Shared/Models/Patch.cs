namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a game patch for Heroes of the Storm.
/// </summary>
public class Patch
{
    public int Id { get; set; }

    /// <summary>
    /// Internal identifier for the patch (e.g., "patch-notes-october-17-2017").
    /// </summary>
    public string InternalId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the patch (e.g., "Junkrat Patch").
    /// </summary>
    public string? PatchName { get; set; }

    /// <summary>
    /// Type of patch: "Patch Notes", "Balance Update", "Hotfix Patch", "PTR".
    /// </summary>
    public string? PatchType { get; set; }

    /// <summary>
    /// Shortened game version (e.g., "28.3").
    /// </summary>
    public string? GameVersion { get; set; }

    /// <summary>
    /// Full version number from the launcher (e.g., "2.28.3.58623").
    /// </summary>
    public string? FullVersion { get; set; }

    /// <summary>
    /// Link to official patch notes blog post.
    /// </summary>
    public string? OfficialLink { get; set; }

    /// <summary>
    /// Alternative link (e.g., BlizzTrack, BlueTracker) when official link is null.
    /// </summary>
    public string? AlternateLink { get; set; }

    /// <summary>
    /// Date the patch went live in North America.
    /// </summary>
    public DateTime? LiveDate { get; set; }

    /// <summary>
    /// Build number for the live patch.
    /// </summary>
    public string? LiveBuild { get; set; }

    /// <summary>
    /// Link to PTR patch notes (if applicable).
    /// </summary>
    public string? PtrOfficialLink { get; set; }

    /// <summary>
    /// Date the patch was released on PTR.
    /// </summary>
    public DateTime? PtrDate { get; set; }

    /// <summary>
    /// Build number for the PTR patch.
    /// </summary>
    public string? PtrBuild { get; set; }

    /// <summary>
    /// Full patch notes content as plain text/markdown.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Original HTML content of the patch notes.
    /// </summary>
    public string? ContentHtml { get; set; }

    /// <summary>
    /// Source of the patch data: "github", "blizzard", "bluetracker".
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Timestamp when this patch data was last synced.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property for patch sections (hero/map-specific changes).
    /// </summary>
    public virtual ICollection<PatchSection> Sections { get; set; } = [];
}
