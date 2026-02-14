namespace HotsPatchNotes.Shared.Models;

/// <summary>
/// Represents an ability for a hero.
/// </summary>
public class Ability
{
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the hero.
    /// </summary>
    public int HeroId { get; set; }

    /// <summary>
    /// Unique identifier for the ability.
    /// </summary>
    public string? Uid { get; set; }

    /// <summary>
    /// Display name of the ability.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full description of the ability.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Hotkey to activate the ability (Q, W, E, R, D, Z, etc.).
    /// </summary>
    public string? Hotkey { get; set; }

    /// <summary>
    /// Internal ability identifier (e.g., "Abathur|Q1").
    /// </summary>
    public string? AbilityId { get; set; }

    /// <summary>
    /// Cooldown in seconds.
    /// </summary>
    public double? Cooldown { get; set; }

    /// <summary>
    /// Mana cost (string due to per-second costs for channeled abilities).
    /// </summary>
    public string? ManaCost { get; set; }

    /// <summary>
    /// Icon file name for the ability.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Type of ability: basic, heroic, trait, mount, or active.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Whether this is a trait ability.
    /// </summary>
    public bool IsTrait { get; set; }

    /// <summary>
    /// The form or context this ability belongs to (e.g., "AbathurSymbiote" for Abathur's symbiote abilities).
    /// </summary>
    public string? FormName { get; set; }

    /// <summary>
    /// Damage scaling per level (e.g., "4%")
    /// </summary>
    public string? Scaling { get; set; }

    /// <summary>
    /// Cast time (e.g., "0.5 seconds")
    /// </summary>
    public string? CastTime { get; set; }

    /// <summary>
    /// Ability range
    /// </summary>
    public string? Range { get; set; }

    /// <summary>
    /// Area of effect description
    /// </summary>
    public string? AreaOfEffect { get; set; }

    /// <summary>
    /// Navigation property to the hero.
    /// </summary>
    public virtual Hero? Hero { get; set; }
}
