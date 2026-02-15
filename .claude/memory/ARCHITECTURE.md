# Architecture: Deep Context

> **🏗️ Maintenance Reminder**: Update this file when project structure changes, database schema evolves, or new data flows are added. Archive old "Recent Implementations" from MEMORY.md here for historical reference.

## Project Structure

### Assembly Organization
```
HeroesOfTheStormPatchNotes/
├── src/
│   ├── HotsPatchNotes.Api/           # ASP.NET Core Web API
│   ├── HotsPatchNotes.Web/           # Blazor WebAssembly SPA
│   └── HotsPatchNotes.Shared/        # Shared models and DTOs
├── tests/
│   └── HotsPatchNotes.Api.Tests/     # xUnit + Moq + EF Core InMemory
└── docs/                              # Documentation
```

### Assembly Dependencies
```
Web (Blazor WASM)
  └─> Shared (Models, DTOs, Constants)

Api (ASP.NET Core)
  ├─> Shared (Models, DTOs, Constants)
  └─> EF Core, HtmlAgilityPack, HtmlSanitizer

Tests
  ├─> Api (Services, Repositories, Controllers)
  ├─> Shared (Models, DTOs)
  └─> xUnit, Moq, EF Core InMemory
```

### HotsPatchNotes.Api Project Structure
```
HotsPatchNotes.Api/
├── Controllers/              # REST API endpoints
│   ├── HeroesController.cs
│   ├── PatchesController.cs
│   ├── BattlegroundsController.cs
│   ├── AdminController.cs
│   └── SyncController.cs
├── Services/                 # Business logic layer
│   ├── HeroService.cs
│   ├── PatchService.cs
│   ├── BattlegroundService.cs
│   ├── GitHubSyncService.cs        # Orchestrates data sync
│   ├── HeroesDataSyncService.cs    # Fetches heroes-data stats
│   ├── S2MAParserService.cs        # Parses S2MA map files
│   ├── BattlegroundScraper.cs      # Scrapes Fandom wiki
│   ├── HeroScraper.cs
│   ├── HtmlContentService.cs       # Sanitizes HTML
│   ├── ImageDownloadService.cs
│   └── BackgroundSyncService.cs    # Hosted service (6-hour interval)
├── Repositories/             # Data access layer
│   ├── HeroRepository.cs
│   ├── PatchRepository.cs
│   ├── BuildRepository.cs
│   └── BattlegroundRepository.cs
├── Data/
│   └── HotsDbContext.cs      # EF Core DbContext
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
└── Program.cs                # DI registration, middleware pipeline
```

### HotsPatchNotes.Web Project Structure
```
HotsPatchNotes.Web/
├── Pages/                    # Razor page components
│   ├── Index.razor
│   ├── HeroList.razor
│   ├── HeroDetail.razor
│   ├── PatchList.razor
│   ├── PatchDetail.razor
│   ├── BattlegroundList.razor
│   └── BuildCalculator.razor
├── Services/                 # HTTP clients calling API
│   ├── HeroApiService.cs
│   ├── PatchApiService.cs
│   └── BattlegroundApiService.cs
└── wwwroot/
    └── appsettings.json      # API base URL configuration
```

### HotsPatchNotes.Shared Project Structure
```
HotsPatchNotes.Shared/
├── Models/                   # Domain entities (EF Core)
│   ├── Hero.cs
│   ├── Ability.cs
│   ├── Talent.cs
│   ├── Patch.cs
│   ├── PatchSection.cs
│   ├── Battleground.cs
│   ├── HeroBuild.cs
│   └── Language.cs          # Enum for localization
├── DTOs/                     # Data transfer objects
│   ├── HeroDtos.cs          # HeroSummaryDto, HeroDetailDto, AbilityDto, TalentDto, etc.
│   ├── PatchDtos.cs
│   ├── BattlegroundDtos.cs
│   └── CommonDtos.cs        # ErrorResponseDto, etc.
└── Constants.cs              # Application-wide constants
```

---

## Data Flow Diagrams

### Request Flow (Blazor → API → Service → Repository → EF Core)
```
User Browser (Blazor WASM)
  ↓ HTTP GET /api/heroes/arthas
HeroesController.GetHeroAsync()
  ↓ Call service layer
HeroService.GetHeroAsync()
  ↓ Call repository layer
HeroRepository.GetByShortNameAsync()
  ↓ EF Core query with .Include()
HotsDbContext (EF Core)
  ↓ SQL query to SQLite/PostgreSQL
Database
  ↓ Return Hero entity with Abilities, Talents
HeroRepository
  ↓ Return Hero entity
HeroService.MapToDetailDto()
  ↓ Return HeroDetailDto
HeroesController
  ↓ Return ActionResult<HeroDetailDto>
User Browser (renders hero detail page)
```

### Multi-Phase Sync Pipeline
```
Background Service (6-hour interval) OR Manual Trigger (POST /api/sync)
  ↓
GitHubSyncService.SyncAllAsync()
  ├─> Phase 1: SyncHeroesFromTalentsRepoAsync()
  │     ↓ Fetch heroespatchnotes/heroes-talents (primary)
  │     ↓ Parse JSON files (heroes, abilities, talents)
  │     ↓ Save to database
  │     ↓ SUCCESS required for sync to continue
  │
  ├─> Phase 2: HeroesDataSyncService.EnhanceHeroDataAsync()
  │     ↓ Fetch HeroesToolChest/heroes-data (supplementary)
  │     ↓ Parse gamedata JSON files
  │     ↓ Enhance existing heroes with extended stats
  │     ↓ OPTIONAL - graceful degradation if fails
  │
  ├─> Phase 3: SyncBattlegroundsAsync()
  │     ├─> S2MAParserService.ParseBattlegroundsAsync()
  │     │     ↓ Fetch jamiephan/HeroesOfTheStorm_S2MA (authoritative)
  │     │     ↓ Parse .StormMap files
  │     │     ↓ Extract metadata (name, type, timing)
  │     │     ↓ OPTIONAL - fallback if missing
  │     │
  │     └─> BattlegroundScraper.ScrapeBattlegroundsAsync()
  │           ↓ Scrape Fandom wiki (fallback)
  │           ↓ Extract descriptions, objectives
  │           ↓ OPTIONAL - used if S2MA data incomplete
  │
  └─> Phase 4: SyncPatchesAsync()
        ↓ Fetch heroespatchnotes/heroes-patch-data
        ↓ Parse patch metadata
        ↓ Scrape BlueTracker for patch notes
        ↓ Save to database
```

---

## Database Schema

### Table Relationships
```
Heroes (1:N Abilities, 1:N Talents, 1:N Builds)
  ├─> Abilities (N:1 Hero)
  ├─> Talents (N:1 Hero)
  └─> Builds (N:1 Hero)

Patches (1:N PatchSections)
  └─> PatchSections (N:1 Patch, N:1 Hero optional)

Battlegrounds (standalone)
```

### Hero Table (Key Columns)
```sql
Heroes
  - Id (PK)
  - ShortName (UNIQUE, indexed) - URL-friendly identifier
  - Name, Title, Role, ExpandedRole, Type
  - Icon, SplashArtUrl, WikiUrl
  - ReleaseDate, ReleasePatch
  - TagsJson (TEXT) - serialized List<string>
  - CountersJson, CounteredByJson, SynergiesJson (TEXT)
  - Description, Lore, TipsJson (TEXT)
  - BaseHealth, HealthRegen, BaseMana, ManaRegen, BaseAttackDamage, AttackSpeed, AttackRange
  - LifeMax, LifeRegenRate, LifeScaling, EnergyMax, ShieldMax, Speed, SightRadius (extended stats)
```

### Ability Table (Key Columns)
```sql
Abilities
  - Id (PK)
  - HeroId (FK to Heroes)
  - Name, Description, AbilityId, Hotkey, Type
  - Cooldown, ManaCost, LifeCost, Icon
  - IsTrait, IsPassive, IsToggle
  - Scaling, CastTime, Range, AreaOfEffect
  - Charges, ChargesMax, RechargeTime
  - FormName (for multi-form heroes like D.Va, Greymane)
```

### Talent Table (Key Columns)
```sql
Talents
  - Id (PK)
  - HeroId (FK to Heroes)
  - Level (1, 4, 7, 10, 13, 16, 20)
  - Sort (order within tier)
  - Name, Description, TooltipId, TalentTreeId, Icon
  - Type, Cooldown, AbilityId
  - AbilityLinksJson (TEXT) - serialized List<string>
  - LinkedAbilityName, Properties
  - IsQuest, QuestRequirement, QuestReward
  - AbilityTalentLinkIdsJson (TEXT)
  - IsStackable
```

### Patch Table (Key Columns)
```sql
Patches
  - Id (PK)
  - InternalId (UNIQUE) - format: "2023-12-05"
  - PatchName, PatchType, GameVersion
  - LiveDate, OfficialLink
```

### PatchSection Table (Key Columns)
```sql
PatchSections
  - Id (PK)
  - PatchId (FK to Patches)
  - HeroId (FK to Heroes, nullable)
  - Order (display order)
  - HeadingLevel (2, 3, 4)
  - SectionType ("Hero", "Battleground", "General")
  - EntityName (hero name or "General Changes")
  - Content (TEXT) - markdown formatted
```

### Battleground Table (Key Columns)
```sql
Battlegrounds
  - Id (PK)
  - ShortName (UNIQUE, indexed)
  - Name, MapType, Universe
  - Description, Objective
  - ObjectiveTiming, IsInRotation
  - WikiUrl, ImageUrl
```

### HeroBuild Table (Key Columns)
```sql
HeroBuilds
  - Id (PK)
  - HeroId (FK to Heroes)
  - Name, Description, TalentCode (7 digits)
  - Source ("user" or "pro")
  - CreatedAt, ViewCount
```

### Indexes
```sql
CREATE INDEX IX_Heroes_ShortName ON Heroes(ShortName);
CREATE INDEX IX_Patches_InternalId ON Patches(InternalId);
CREATE INDEX IX_Battlegrounds_ShortName ON Battlegrounds(ShortName);
CREATE INDEX IX_PatchSections_PatchId ON PatchSections(PatchId);
CREATE INDEX IX_PatchSections_HeroId ON PatchSections(HeroId);
CREATE INDEX IX_Abilities_HeroId ON Abilities(HeroId);
CREATE INDEX IX_Talents_HeroId_Level ON Talents(HeroId, Level);
CREATE INDEX IX_Builds_HeroId ON HeroBuilds(HeroId);
```

---

## Service Layer Deep Dive

### GitHubSyncService Orchestration
**Purpose**: Coordinates multi-phase sync from various data sources.

**Key methods**:
- `SyncAllAsync()` - Entry point, orchestrates all phases
- `SyncHeroesFromTalentsRepoAsync()` - Phase 1 (critical)
- `SyncPatchesAsync()` - Fetch patch metadata + scrape notes
- `SyncBattlegroundsAsync()` - Delegate to S2MAParserService + BattlegroundScraper

**Dependencies**:
- IHeroRepository, IPatchRepository, IBattlegroundRepository
- IHeroesDataSyncService (Phase 2)
- IS2MAParserService (Phase 3)
- IBattlegroundScraper (fallback)
- HttpClient (fetch GitHub repos)

### HeroesDataSyncService Enhancement Pattern
**Purpose**: Enhance existing heroes with extended stats from HeroesToolChest/heroes-data.

**Key methods**:
- `EnhanceHeroDataAsync()` - Entry point
- `FetchHeroDataAsync()` - Fetch gamedata JSON from GitHub
- `MapExtendedStatsToHero()` - Map JSON to Hero entity

**Graceful Degradation**: If fetch fails, existing hero data remains usable (all extended fields nullable).

### S2MAParserService Parsing Logic
**Purpose**: Parse S2MA .StormMap files to extract authoritative battleground metadata.

**Key methods**:
- `ParseBattlegroundsAsync()` - Entry point
- `FetchS2MAFileAsync()` - Fetch .StormMap XML/text from GitHub
- `ParseMapName()`, `ParseMapType()`, `ParseObjectiveTiming()` - Extract metadata

**Fallback**: If S2MA file missing, GitHubSyncService calls BattlegroundScraper.

### BattlegroundScraper Fallback Strategy
**Purpose**: Scrape Fandom wiki for battleground descriptions (fallback only).

**Key methods**:
- `ScrapeBattlegroundsAsync()` - Entry point
- `GetWikiUrl()` - Generate wiki URL from battleground name
- `ParseHtml()` - Use HtmlAgilityPack to extract data

**Fragility**: Web scraping is fragile. DOM changes break scraper. S2MA is preferred.

---

## Deployment & Configuration

### appsettings.json Structure
```json
{
  "DatabaseProvider": "SQLite",  // or "PostgreSQL"
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=hots.db"  // or PostgreSQL connection string
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Environment Variables (Docker)
```bash
DatabaseProvider=PostgreSQL
ConnectionStrings__DefaultConnection=Host=db;Database=hots;Username=user;Password=pass
```

### Docker Compose Setup
```yaml
services:
  api:
    build: .
    ports:
      - "7001:8080"
    environment:
      - DatabaseProvider=PostgreSQL
      - ConnectionStrings__DefaultConnection=Host=db;Database=hots
    depends_on:
      - db

  db:
    image: postgres:15
    environment:
      POSTGRES_DB: hots
      POSTGRES_USER: user
      POSTGRES_PASSWORD: pass
```

### CORS Policies

**Development (AllowAll)**:
```csharp
policy.AllowAnyOrigin()
      .AllowAnyHeader()
      .AllowAnyMethod();
```

**Production (AllowBlazorClient)**:
```csharp
policy.WithOrigins(
        "http://localhost:5100",
        "https://localhost:7000"
      )
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials();
```

### Middleware Pipeline Order (Program.cs)
```
1. GlobalExceptionMiddleware (must be first to catch all exceptions)
2. Swagger (Development only)
3. CORS
4. HttpsRedirection
5. StaticFiles (for downloaded images)
6. ResponseCaching
7. Authorization
8. MapControllers
```

---

## Background Sync Service

### BackgroundSyncService (Hosted Service)
**Purpose**: Automatically sync data from GitHub every 6 hours.

**Implementation**:
```csharp
public class BackgroundSyncService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);

            using var scope = serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();
            await syncService.SyncAllAsync(stoppingToken);
        }
    }
}
```

**Manual Trigger**: `POST /api/sync` (via SyncController)

---

## Key Design Decisions

### 1. Repository Pattern Over Direct DbContext
**Why**: Separation of concerns, testability, abstraction over data access.

### 2. Manual DTO Mapping Over AutoMapper
**Why**: Explicit control, no magic, better performance, easier debugging.

### 3. JSON Columns Over Separate Tables
**Why**: Tags, counters, synergies are simple lists (no querying needed). Simpler schema.

### 4. In-Memory DB for Tests Over Mocking Repositories
**Why**: Test real EF Core queries, avoid fragile mocks, ensure query correctness.

### 5. Multi-Phase Sync with Graceful Degradation
**Why**: External data sources may fail. Primary data (heroes-talents) is critical, supplementary data (heroes-data, S2MA) is optional.

### 6. Primary Constructors + Sealed Classes
**Why**: Modern C# 13 pattern, concise DI, prevents inheritance (composition over inheritance).
