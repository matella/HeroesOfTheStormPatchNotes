namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a Heroes of the Storm battleground (map).
/// </summary>
public class Battleground
{
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the battleground (e.g., "alterac-pass")
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Alterac Pass")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Map type (e.g., "3-Lane", "2-Lane")
    /// </summary>
    public string? MapType { get; set; }

    /// <summary>
    /// Short description of the map
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Primary map objective description
    /// </summary>
    public string? Objective { get; set; }

    /// <summary>
    /// When the objective activates (e.g., "3:00 minutes")
    /// </summary>
    public string? ObjectiveTiming { get; set; }

    /// <summary>
    /// Information about mercenary camps
    /// </summary>
    public string? MercCamps { get; set; }

    /// <summary>
    /// Boss information if applicable
    /// </summary>
    public string? BossInfo { get; set; }

    /// <summary>
    /// Tips and strategies for the map
    /// </summary>
    public string? Tips { get; set; }

    /// <summary>
    /// Image filename or URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Whether the map is currently in the ranked rotation
    /// </summary>
    public bool IsInRotation { get; set; } = true;

    /// <summary>
    /// When the map was added to the game
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Event associated with the map (if any)
    /// </summary>
    public string? Event { get; set; }

    /// <summary>
    /// Universe/franchise (e.g., "Warcraft", "Diablo", "Nexus")
    /// </summary>
    public string? Universe { get; set; }
}
