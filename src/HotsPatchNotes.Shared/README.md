# HotsPatchNotes.Shared

Shared library containing domain models and DTOs used by both the API and Web projects.

## Overview

This project provides:
- **Domain Models** - Entity classes for Entity Framework Core
- **DTOs** - Data Transfer Objects for API communication
- **Common Types** - Shared response types and utilities

## Project Structure

```
HotsPatchNotes.Shared/
├── Models/
│   ├── Hero.cs        # Hero entity with navigation properties
│   ├── Ability.cs     # Hero ability entity
│   ├── Talent.cs      # Hero talent entity
│   └── Patch.cs       # Game patch entity
└── DTOs/
    ├── HeroDtos.cs    # HeroSummaryDto, HeroDetailDto, AbilityDto, TalentDto
    ├── PatchDtos.cs   # PatchSummaryDto, PatchDetailDto
    └── CommonDtos.cs  # SyncResultDto, PagedResultDto, ErrorDto
```

## Domain Models

### Hero

Represents a playable hero in Heroes of the Storm.

```csharp
public class Hero
{
    public int Id { get; set; }
    public string ShortName { get; set; }      // e.g., "abathur", "li-ming"
    public string Name { get; set; }           // e.g., "Abathur", "Li-Ming"
    public string? Icon { get; set; }          // e.g., "abathur.png"
    public string? Role { get; set; }          // e.g., "Specialist", "Assassin"
    public string? ExpandedRole { get; set; }  // e.g., "Support", "Burst Damage"
    public string? Type { get; set; }          // "Melee" or "Ranged"
    public DateTime? ReleaseDate { get; set; }
    public string? TagsJson { get; set; }      // JSON array of tags
    
    // Navigation properties
    public List<Ability> Abilities { get; set; }
    public List<Talent> Talents { get; set; }
}
```

### Ability

Represents a hero's ability (Q, W, E, R, Trait, Mount).

```csharp
public class Ability
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Hotkey { get; set; }        // "Q", "W", "E", "R", "Z"
    public double? Cooldown { get; set; }
    public string? ManaCost { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }          // "basic", "heroic", "trait", "mount"
    public bool IsTrait { get; set; }
    public string? FormName { get; set; }      // Groups abilities by hero form
    
    public Hero Hero { get; set; }
}
```

**Note on FormName:** Some heroes have multiple ability sets. For example, Abathur has:
- `"Abathur"` - Base abilities (Symbiote, Toxic Nest, etc.)
- `"AbathurSymbiote"` - Symbiote abilities (Stab, Spike Burst, Carapace)

### Talent

Represents a talent choice at a specific level.

```csharp
public class Talent
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public int Level { get; set; }             // 1, 4, 7, 10, 13, 16, or 20
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Type { get; set; }          // Related ability type
    public int Sort { get; set; }              // Display order within tier
    public double? Cooldown { get; set; }
    public string? AbilityLinksJson { get; set; } // JSON array of linked abilities
    
    public Hero Hero { get; set; }
}
```

### Patch

Represents a game patch or update.

```csharp
public class Patch
{
    public int Id { get; set; }
    public string InternalId { get; set; }     // e.g., "balance-update-notes-march-2-2021"
    public string? PatchName { get; set; }     // e.g., "Hogger Balance Patch #2"
    public string? PatchType { get; set; }     // e.g., "Balance Update", "Major Patch"
    public string? GameVersion { get; set; }   // e.g., "53.2"
    public string? FullVersion { get; set; }   // Full version string
    public string? OfficialLink { get; set; }  // Blizzard patch notes URL
    public string? AlternateLink { get; set; } // Reddit/community link
    public DateTime? LiveDate { get; set; }
    public DateTime? PtrDate { get; set; }     // PTR release date
}
```

## DTOs (Data Transfer Objects)

### Hero DTOs

**HeroSummaryDto** - Used in hero list views:
```csharp
public class HeroSummaryDto
{
    public int Id { get; set; }
    public string ShortName { get; set; }
    public string Name { get; set; }
    public string? Icon { get; set; }
    public string? Role { get; set; }
    public string? ExpandedRole { get; set; }
    public string? Type { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public List<string> Tags { get; set; }
}
```

**HeroDetailDto** - Used in hero detail views:
```csharp
public class HeroDetailDto
{
    public int Id { get; set; }
    public string ShortName { get; set; }
    public string Name { get; set; }
    // ... other properties
    
    // Abilities grouped by FormName
    public Dictionary<string, List<AbilityDto>> Abilities { get; set; }
    
    // Talents grouped by Level
    public Dictionary<int, List<TalentDto>> Talents { get; set; }
}
```

### Patch DTOs

**PatchSummaryDto** - Used in patch list views:
```csharp
public class PatchSummaryDto
{
    public int Id { get; set; }
    public string InternalId { get; set; }
    public string? PatchName { get; set; }
    public string? PatchType { get; set; }
    public string? GameVersion { get; set; }
    public DateTime? LiveDate { get; set; }
    public string? OfficialLink { get; set; }
    public string? AlternateLink { get; set; }
}
```

### Common DTOs

**PagedResultDto<T>** - Paginated API response:
```csharp
public class PagedResultDto<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}
```

**SyncResultDto** - Data sync operation result:
```csharp
public class SyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public int HeroesUpdated { get; set; }
    public int PatchesUpdated { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<string> Errors { get; set; }
}
```

## Usage

### In the API Project

Models are used with Entity Framework Core:

```csharp
// DbContext
public DbSet<Hero> Heroes { get; set; }
public DbSet<Ability> Abilities { get; set; }
public DbSet<Talent> Talents { get; set; }
public DbSet<Patch> Patches { get; set; }

// Querying
var hero = await _dbContext.Heroes
    .Include(h => h.Abilities)
    .Include(h => h.Talents)
    .FirstOrDefaultAsync(h => h.ShortName == "abathur");
```

### In the Web Project

DTOs are used for API responses:

```csharp
// Fetching heroes
var heroes = await _httpClient.GetFromJsonAsync<List<HeroSummaryDto>>("/api/heroes");

// Fetching a specific hero
var hero = await _httpClient.GetFromJsonAsync<HeroDetailDto>($"/api/heroes/{shortName}");

// Fetching paginated patches
var patches = await _httpClient.GetFromJsonAsync<PagedResultDto<PatchSummaryDto>>("/api/patches");
```

## JSON Storage

Some fields use JSON for flexible storage:

- **Hero.TagsJson** - Stores tags as JSON array: `["Ganker", "Helper", "WaveClearer"]`
- **Talent.AbilityLinksJson** - Stores linked ability IDs as JSON array

Helper methods for working with these fields:

```csharp
// Reading tags
var tags = hero.TagsJson != null 
    ? JsonSerializer.Deserialize<List<string>>(hero.TagsJson) 
    : new List<string>();

// Writing tags
hero.TagsJson = JsonSerializer.Serialize(tags);
```

## Dependencies

- **System.Text.Json** - JSON serialization (included in .NET)

This project has no external dependencies, making it lightweight and easy to reference.
