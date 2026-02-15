# Phase 3: Localization Foundation - Implementation Plan

**Status:** Deferred (Foundation in place, full implementation pending)
**Created:** 2026-02-14
**Prerequisites:** Phase 1 (heroes-data) and Phase 2 (S2MA) completed

---

## Executive Summary

This document outlines the plan for implementing multi-language support (internationalization/i18n) in the Heroes of the Storm Patch Notes application. The foundation has been laid with the `Language` enum and heroes-data integration. This plan can be executed when international user demand justifies the implementation effort.

---

## Current State (What's Already Done)

### ✅ Foundation Complete

1. **Language Enum Created** (`src/HotsPatchNotes.Shared/Models/Language.cs`)
   - Supports 11 languages: English, German, Spanish, French, Italian, Polish, Portuguese, Russian, Korean, Chinese (Simplified & Traditional)
   - Default: English (Language = 0)

2. **Data Source Integration**
   - HeroesToolChest/heroes-data provides localized JSON files
   - Format: `herodata_<version>_enus.json`, `herodata_<version>_kokr.json`, etc.
   - Available languages match our Language enum

3. **Model Architecture**
   - All models designed with nullable fields (supports partial localization)
   - DTOs expose all fields (ready for language-specific content)

---

## Implementation Approach

### Option A: Localized Content Tables (Recommended)

**Design Pattern:** Separate normalized tables for localized content

#### Schema Design

```sql
-- Example: Hero localized content
CREATE TABLE HeroLocalizedContent (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    HeroId INT NOT NULL,
    Language INT NOT NULL, -- Language enum (0=English, 8=Korean, etc.)
    Name NVARCHAR(200) NOT NULL,
    Title NVARCHAR(200),
    Description NTEXT,
    Lore NTEXT,
    FOREIGN KEY (HeroId) REFERENCES Heroes(Id),
    UNIQUE KEY (HeroId, Language)
);

-- Example: Ability localized content
CREATE TABLE AbilityLocalizedContent (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    AbilityId INT NOT NULL,
    Language INT NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NTEXT,
    FOREIGN KEY (AbilityId) REFERENCES Abilities(Id),
    UNIQUE KEY (AbilityId, Language)
);

-- Example: Talent localized content
CREATE TABLE TalentLocalizedContent (
    Id INT PRIMARY KEY AUTO_INCREMENT,
    TalentId INT NOT NULL,
    Language INT NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NTEXT,
    QuestRequirement NTEXT,
    QuestReward NTEXT,
    FOREIGN KEY (TalentId) REFERENCES Talents(Id),
    UNIQUE KEY (TalentId, Language)
);
```

#### Model Classes

```csharp
// src/HotsPatchNotes.Shared/Models/HeroLocalizedContent.cs
public class HeroLocalizedContent
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public Language Language { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Lore { get; set; }

    public virtual Hero? Hero { get; set; }
}
```

#### Pros:
- ✅ Normalized, efficient queries
- ✅ Easy to add new languages
- ✅ Supports partial localization (missing translations fall back to English)
- ✅ Clear separation of concerns

#### Cons:
- ❌ More complex queries (JOINs required)
- ❌ Multiple tables to maintain
- ❌ More database migrations

---

### Option B: JSON Columns for Localized Text (Alternative)

**Design Pattern:** Store localized content as JSON in existing tables

#### Schema Design

```csharp
// Add to existing Hero model
public class Hero
{
    // ... existing fields ...

    /// <summary>
    /// JSON dictionary of localized names { "en": "Arthas", "ko": "아서스", "zh": "阿尔萨斯" }
    /// </summary>
    public string? LocalizedNamesJson { get; set; }

    /// <summary>
    /// JSON dictionary of localized descriptions
    /// </summary>
    public string? LocalizedDescriptionsJson { get; set; }
}
```

#### Pros:
- ✅ Simpler schema (no new tables)
- ✅ Fewer database queries
- ✅ Easy to implement

#### Cons:
- ❌ Harder to query specific languages
- ❌ JSON parsing overhead
- ❌ Less type-safe
- ❌ Denormalized (data duplication)

---

## Recommended Approach: Option A (Localized Content Tables)

**Rationale:**
- Better long-term maintainability
- More efficient for language-specific queries
- Follows database normalization principles
- Aligns with .NET Entity Framework best practices

---

## Implementation Steps

### Step 1: Database Schema Changes

**Create Migration:**

```csharp
// Add-Migration AddLocalizationSupport

public partial class AddLocalizationSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Create HeroLocalizedContent table
        migrationBuilder.CreateTable(
            name: "HeroLocalizedContent",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                HeroId = table.Column<int>(nullable: false),
                Language = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Title = table.Column<string>(maxLength: 200, nullable: true),
                Description = table.Column<string>(nullable: true),
                Lore = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HeroLocalizedContent", x => x.Id);
                table.ForeignKey(
                    name: "FK_HeroLocalizedContent_Heroes_HeroId",
                    column: x => x.HeroId,
                    principalTable: "Heroes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Create unique index on (HeroId, Language)
        migrationBuilder.CreateIndex(
            name: "IX_HeroLocalizedContent_HeroId_Language",
            table: "HeroLocalizedContent",
            columns: new[] { "HeroId", "Language" },
            unique: true);

        // Repeat for AbilityLocalizedContent, TalentLocalizedContent, etc.
    }
}
```

### Step 2: Extend Models

**Create New Models:**

```csharp
// src/HotsPatchNotes.Shared/Models/HeroLocalizedContent.cs
public class HeroLocalizedContent
{
    public int Id { get; set; }
    public int HeroId { get; set; }
    public Language Language { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Lore { get; set; }

    public virtual Hero? Hero { get; set; }
}

// src/HotsPatchNotes.Shared/Models/AbilityLocalizedContent.cs
public class AbilityLocalizedContent
{
    public int Id { get; set; }
    public int AbilityId { get; set; }
    public Language Language { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual Ability? Ability { get; set; }
}

// src/HotsPatchNotes.Shared/Models/TalentLocalizedContent.cs
public class TalentLocalizedContent
{
    public int Id { get; set; }
    public int TalentId { get; set; }
    public Language Language { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? QuestRequirement { get; set; }
    public string? QuestReward { get; set; }

    public virtual Talent? Talent { get; set; }
}
```

**Update Existing Models:**

```csharp
// Add to Hero.cs
public virtual ICollection<HeroLocalizedContent> LocalizedContent { get; set; } = [];

// Add to Ability.cs
public virtual ICollection<AbilityLocalizedContent> LocalizedContent { get; set; } = [];

// Add to Talent.cs
public virtual ICollection<TalentLocalizedContent> LocalizedContent { get; set; } = [];
```

### Step 3: Update DbContext

```csharp
// src/HotsPatchNotes.Api/Data/HotsDbContext.cs

public DbSet<HeroLocalizedContent> HeroLocalizedContent { get; set; }
public DbSet<AbilityLocalizedContent> AbilityLocalizedContent { get; set; }
public DbSet<TalentLocalizedContent> TalentLocalizedContent { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure HeroLocalizedContent
    modelBuilder.Entity<HeroLocalizedContent>(entity =>
    {
        entity.HasIndex(e => new { e.HeroId, e.Language }).IsUnique();
        entity.Property(e => e.Language).HasConversion<int>();
    });

    // Configure AbilityLocalizedContent
    modelBuilder.Entity<AbilityLocalizedContent>(entity =>
    {
        entity.HasIndex(e => new { e.AbilityId, e.Language }).IsUnique();
        entity.Property(e => e.Language).HasConversion<int>();
    });

    // Configure TalentLocalizedContent
    modelBuilder.Entity<TalentLocalizedContent>(entity =>
    {
        entity.HasIndex(e => new { e.TalentId, e.Language }).IsUnique();
        entity.Property(e => e.Language).HasConversion<int>();
    });
}
```

### Step 4: Create LocalizedHeroesDataSyncService

**Purpose:** Fetch and sync localized hero data from heroes-data repository

```csharp
// src/HotsPatchNotes.Api/Services/LocalizedHeroesDataSyncService.cs

public sealed class LocalizedHeroesDataSyncService(
    HotsDbContext dbContext,
    HttpClient httpClient,
    ILogger<LocalizedHeroesDataSyncService> logger) : ILocalizedHeroesDataSyncService
{
    // Language code mapping
    private static readonly Dictionary<Language, string> LanguageCodes = new()
    {
        [Language.English] = "enus",
        [Language.German] = "dede",
        [Language.Spanish] = "eses",
        [Language.French] = "frfr",
        [Language.Italian] = "itit",
        [Language.Polish] = "plpl",
        [Language.Portuguese] = "ptbr",
        [Language.Russian] = "ruru",
        [Language.Korean] = "kokr",
        [Language.ChineseSimplified] = "zhcn",
        [Language.ChineseTraditional] = "zhtw"
    };

    public async Task<SyncResultDto> SyncLocalizedContentAsync(
        Language language,
        CancellationToken cancellationToken = default)
    {
        var result = new SyncResultDto { SyncedAt = DateTime.UtcNow };

        try
        {
            var languageCode = LanguageCodes[language];
            var fileName = $"herodata_<version>_{languageCode}.json";

            // Download localized JSON from heroes-data
            var fileUrl = $"{HeroesDataRawBaseUrl}/{fileName}";
            var jsonContent = await httpClient.GetStringAsync(fileUrl, cancellationToken);
            var heroesData = JsonSerializer.Deserialize<Dictionary<string, HeroesDataHero>>(jsonContent);

            // Parse and save localized content
            foreach (var (heroId, heroData) in heroesData)
            {
                var hero = await dbContext.Heroes
                    .FirstOrDefaultAsync(h => h.HyperlinkId == heroId, cancellationToken);

                if (hero is null) continue;

                // Save localized hero content
                var localizedContent = new HeroLocalizedContent
                {
                    HeroId = hero.Id,
                    Language = language,
                    Name = heroData.Name,
                    Title = heroData.Title,
                    Description = heroData.Description,
                    Lore = heroData.Lore
                };

                dbContext.HeroLocalizedContent.Add(localizedContent);

                // Save localized abilities
                if (heroData.Abilities is not null)
                {
                    foreach (var (abilityId, abilityData) in heroData.Abilities)
                    {
                        var ability = hero.Abilities.FirstOrDefault(a => a.AbilityId == abilityId);
                        if (ability is null) continue;

                        var localizedAbility = new AbilityLocalizedContent
                        {
                            AbilityId = ability.Id,
                            Language = language,
                            Name = abilityData.Name,
                            Description = abilityData.Description
                        };

                        dbContext.AbilityLocalizedContent.Add(localizedAbility);
                    }
                }

                // Save localized talents
                if (heroData.Talents is not null)
                {
                    foreach (var (talentId, talentData) in heroData.Talents)
                    {
                        var talent = hero.Talents.FirstOrDefault(t => t.TalentTreeId == talentId);
                        if (talent is null) continue;

                        var localizedTalent = new TalentLocalizedContent
                        {
                            TalentId = talent.Id,
                            Language = language,
                            Name = talentData.Name,
                            Description = talentData.Description
                        };

                        dbContext.TalentLocalizedContent.Add(localizedTalent);
                    }
                }

                result.HeroesUpdated++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            result.Success = true;
            result.Message = $"Synced {result.HeroesUpdated} heroes with {language} localization";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync localized content for {Language}", language);
            result.Errors.Add($"Localization sync failed: {ex.Message}");
        }

        return result;
    }
}
```

### Step 5: Update DTOs for Language Selection

```csharp
// Extend HeroDetailDto to support language selection
public class HeroDetailDto
{
    // ... existing fields ...

    /// <summary>
    /// Language for localized content (defaults to English)
    /// </summary>
    public Language Language { get; set; } = Language.English;

    /// <summary>
    /// Available languages for this hero
    /// </summary>
    public List<Language> AvailableLanguages { get; set; } = [];
}
```

### Step 6: Update API Endpoints

```csharp
// Add language query parameter to hero endpoints
[HttpGet("{shortName}")]
[ResponseCache(Duration = 3600)]
public async Task<ActionResult<HeroDetailDto>> GetHero(
    string shortName,
    [FromQuery] Language language = Language.English,
    CancellationToken cancellationToken = default)
{
    var hero = await heroService.GetHeroAsync(shortName, language, cancellationToken);

    if (hero is null)
        return NotFound(new ErrorResponseDto { Message = Constants.ErrorMessages.HeroNotFound });

    return Ok(hero);
}
```

### Step 7: Update HeroService Mapping

```csharp
// Modify MapToDetailDto to use localized content
private async Task<HeroDetailDto> MapToDetailDto(Hero hero, Language language)
{
    var dto = new HeroDetailDto { /* ... base mapping ... */ };

    // Use localized content if available
    var localizedContent = hero.LocalizedContent
        .FirstOrDefault(lc => lc.Language == language);

    if (localizedContent is not null)
    {
        dto.Name = localizedContent.Name;
        dto.Title = localizedContent.Title;
        dto.Description = localizedContent.Description;
        dto.Lore = localizedContent.Lore;
    }

    // Map abilities with localization
    foreach (var ability in hero.Abilities)
    {
        var localizedAbility = ability.LocalizedContent
            .FirstOrDefault(lc => lc.Language == language);

        var abilityDto = new AbilityDto
        {
            Name = localizedAbility?.Name ?? ability.Name,
            Description = localizedAbility?.Description ?? ability.Description,
            // ... other fields ...
        };
    }

    // Map talents with localization
    // ... similar pattern ...

    // Set available languages
    dto.AvailableLanguages = hero.LocalizedContent
        .Select(lc => lc.Language)
        .Distinct()
        .ToList();

    return dto;
}
```

---

## API Changes

### New Endpoints

```http
GET /api/heroes/{shortName}?language=Korean
GET /api/heroes/{shortName}?language=ChineseSimplified

GET /api/heroes?language=Korean&role=Tank
```

### Response Format (Example)

```json
{
  "id": 1,
  "shortName": "arthas",
  "name": "아서스",
  "title": "리치 왕",
  "language": "Korean",
  "availableLanguages": ["English", "Korean", "ChineseSimplified"],
  "abilities": {
    "Arthas": [
      {
        "name": "죽음의 고리",
        "description": "범위 내의 적들에게 피해를 주고 이동 속도를 감소시킵니다.",
        "hotkey": "Q"
      }
    ]
  }
}
```

---

## Blazor Web Changes

### Language Selector Component

```razor
<!-- src/HotsPatchNotes.Web/Components/LanguageSelector.razor -->
<div class="language-selector">
    <label for="language">Language:</label>
    <select id="language" @bind="SelectedLanguage" @bind:after="OnLanguageChanged">
        <option value="@Language.English">English</option>
        <option value="@Language.Korean">한국어 (Korean)</option>
        <option value="@Language.ChineseSimplified">简体中文 (Chinese)</option>
        <!-- Add other languages as needed -->
    </select>
</div>

@code {
    [Parameter]
    public Language SelectedLanguage { get; set; } = Language.English;

    [Parameter]
    public EventCallback<Language> SelectedLanguageChanged { get; set; }

    private async Task OnLanguageChanged()
    {
        await SelectedLanguageChanged.InvokeAsync(SelectedLanguage);
    }
}
```

### Store Language Preference

```csharp
// src/HotsPatchNotes.Web/Services/LanguageService.cs
public class LanguageService
{
    private const string LanguageKey = "preferred_language";

    public Language GetPreferredLanguage()
    {
        var stored = localStorage.GetItem(LanguageKey);
        return Enum.TryParse<Language>(stored, out var language)
            ? language
            : Language.English;
    }

    public void SetPreferredLanguage(Language language)
    {
        localStorage.SetItem(LanguageKey, language.ToString());
    }
}
```

---

## Priority Languages (Based on HotS Player Base)

1. **English** (en-US) - Default, already supported
2. **Korean** (ko-KR) - Large competitive scene, high priority
3. **Chinese Simplified** (zh-CN) - Large player base
4. **Chinese Traditional** (zh-TW) - Taiwan/Hong Kong players
5. **German** (de-DE) - European player base
6. **French** (fr-FR) - European player base
7. **Spanish** (es-ES) - European/Latin American players
8. **Russian** (ru-RU) - Eastern European players

**Recommendation:** Implement Korean and Chinese first if pursuing localization.

---

## Testing Checklist

### Database Tests
- [ ] Migration creates tables correctly
- [ ] Unique constraint on (EntityId, Language) works
- [ ] Cascade delete removes localized content when entity deleted
- [ ] Language enum correctly converts to/from integer

### Service Tests
- [ ] LocalizedHeroesDataSyncService fetches correct language files
- [ ] Falls back to English when translation missing
- [ ] Handles missing localized content gracefully
- [ ] Concurrent language syncs don't conflict

### API Tests
- [ ] Language query parameter works correctly
- [ ] Returns English by default
- [ ] Returns 404 for invalid language
- [ ] AvailableLanguages list is accurate

### Frontend Tests
- [ ] Language selector persists preference
- [ ] Content updates when language changes
- [ ] Fallback to English when translation missing
- [ ] No layout issues with longer translated text

---

## Performance Considerations

### Caching Strategy

```csharp
// Cache localized content aggressively
[ResponseCache(Duration = 7200, VaryByQueryKeys = new[] { "language" })]
public async Task<ActionResult<HeroDetailDto>> GetHero(...)
```

### Database Indexing

```sql
-- Essential indexes for performance
CREATE INDEX IX_HeroLocalizedContent_HeroId ON HeroLocalizedContent(HeroId);
CREATE INDEX IX_HeroLocalizedContent_Language ON HeroLocalizedContent(Language);
CREATE UNIQUE INDEX IX_HeroLocalizedContent_HeroId_Language ON HeroLocalizedContent(HeroId, Language);
```

### Lazy Loading vs Eager Loading

```csharp
// Eager load localized content for language-specific queries
var hero = await dbContext.Heroes
    .Include(h => h.Abilities)
        .ThenInclude(a => a.LocalizedContent.Where(lc => lc.Language == language))
    .Include(h => h.Talents)
        .ThenInclude(t => t.LocalizedContent.Where(lc => lc.Language == language))
    .Include(h => h.LocalizedContent.Where(lc => lc.Language == language))
    .FirstOrDefaultAsync(h => h.ShortName == shortName);
```

---

## Estimated Effort

- **Database Schema:** 2-4 hours
- **Model & DbContext Updates:** 2-3 hours
- **LocalizedHeroesDataSyncService:** 4-6 hours
- **API Updates (DTOs, Endpoints, Mapping):** 4-6 hours
- **Blazor UI (Language Selector, State Management):** 3-4 hours
- **Testing:** 4-6 hours
- **Documentation:** 2 hours

**Total:** 21-31 hours (~3-4 days of focused work)

---

## Rollout Strategy

### Phase 3.1: English + Korean (Pilot)
- Implement full infrastructure
- Sync only English and Korean
- Validate approach with 2 languages

### Phase 3.2: Add Chinese (Simplified & Traditional)
- Extend to Chinese languages
- Validate CJK character handling

### Phase 3.3: Add European Languages
- Add German, French, Spanish, etc.
- Full localization coverage

---

## Known Challenges

### Challenge 1: heroes-data Version Tracking
**Problem:** Different language files may be from different game versions
**Solution:** Track version per language, sync all languages from same version

### Challenge 2: Missing Translations
**Problem:** Not all content may have translations
**Solution:** Always fall back to English, mark untranslated content in UI

### Challenge 3: Text Length Variations
**Problem:** Translated text may be much longer (e.g., German compounds)
**Solution:** Design UI with flexible layouts, test with longest languages

### Challenge 4: Right-to-Left Languages
**Problem:** Arabic/Hebrew would need RTL layout
**Solution:** Not applicable for HotS (no RTL languages in game)

---

## Decision Points

When implementing Phase 3, decide on:

1. **Which languages to support initially?**
   - Recommend: English, Korean, Chinese (Simplified)

2. **How to handle missing translations?**
   - Recommend: Fall back to English, show indicator

3. **Should localization be per-user or per-request?**
   - Recommend: Per-user preference (stored in localStorage)

4. **Do we localize patch notes content?**
   - Recommend: No (patch notes are from Blizzard in English only)

5. **Do we localize UI strings (buttons, labels)?**
   - Recommend: Yes, use .NET resource files (.resx)

---

## Resources

### Data Sources
- [heroes-data Localized Files](https://github.com/HeroesToolChest/heroes-data)
- Language codes: enus, dede, eses, frfr, itit, kokr, plpl, ptbr, ruru, zhcn, zhtw

### .NET Localization
- [ASP.NET Core Globalization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization)
- [Blazor Localization](https://learn.microsoft.com/en-us/aspnet/core/blazor/globalization-localization)

### Entity Framework
- [EF Core Owned Entity Types](https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities)
- [EF Core Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)

---

## Conclusion

Phase 3 localization infrastructure is well-designed and ready for implementation when needed. The foundation (Language enum, heroes-data integration) is in place. Implementation can proceed incrementally:

1. Start with database schema
2. Add sync for one non-English language (Korean recommended)
3. Update API to support language parameter
4. Add Blazor language selector
5. Expand to additional languages

**Recommendation:** Implement when user demand justifies the 3-4 day effort, or when international partnerships/communities request it.

---

**Last Updated:** 2026-02-14
**Status:** Plan complete, implementation deferred
**Next Steps:** Monitor user requests for multi-language support
