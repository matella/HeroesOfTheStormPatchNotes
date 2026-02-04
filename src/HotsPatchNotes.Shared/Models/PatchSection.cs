namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents a section of patch notes specific to a hero, map, or category.
/// Allows querying patch history for individual heroes/maps.
/// </summary>
public class PatchSection
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent patch.
    /// </summary>
    public int PatchId { get; set; }

    /// <summary>
    /// Navigation property to the parent patch.
    /// </summary>
    public virtual Patch Patch { get; set; } = null!;

    /// <summary>
    /// Order of this section within the patch for proper reconstruction.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Heading level (1-4 for #, ##, ###, ####).
    /// </summary>
    public int HeadingLevel { get; set; }

    /// <summary>
    /// Optional parent section ID for hierarchy tracking.
    /// </summary>
    public int? ParentSectionId { get; set; }

    /// <summary>
    /// Navigation property to parent section.
    /// </summary>
    public virtual PatchSection? ParentSection { get; set; }

    /// <summary>
    /// Child sections (e.g., heroes under Balance).
    /// </summary>
    public virtual ICollection<PatchSection> ChildSections { get; set; } = new List<PatchSection>();

    /// <summary>
    /// Type of section: "Hero", "Map", "General", "Balance", "BugFix".
    /// </summary>
    public string SectionType { get; set; } = string.Empty;

    /// <summary>
    /// Name of the entity this section relates to (hero name, map name, or category).
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Optional foreign key to Hero table (null for maps/general sections).
    /// </summary>
    public int? HeroId { get; set; }

    /// <summary>
    /// Navigation property to the hero (if this is a hero section).
    /// </summary>
    public virtual Hero? Hero { get; set; }

    /// <summary>
    /// Patch notes content for this section as markdown (without the heading line).
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
