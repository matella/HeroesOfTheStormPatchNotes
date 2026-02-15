# Project Memory: Heroes of the Storm Patch Notes

## Current Status (2026-02-15)
- **Tests**: 146 passing ✅
- **Branch**: main
- **Stack**: .NET 10.0 / C# 13 / Blazor WASM / ASP.NET Core
- **Database**: SQLite (default) or PostgreSQL
- **Recent Work**: Phases 1-2 complete (heroes-data + S2MA integration)

## Quick References
- **Code Patterns**: See `QUICK_REFERENCE.md` for DI registry, DTO mapping, controller/repo/test templates
- **Gotchas**: See `GOTCHAS.md` for common pitfalls and debugging tips
- **Architecture**: See `ARCHITECTURE.md` for deep project structure and data flows
- **Code Style**: See `/CLAUDE.md` (authoritative conventions)

## Memory Maintenance Instructions

**IMPORTANT**: Keep these memory files up-to-date as you work on the project.

### When to Update

**Update MEMORY.md when**:
- Test count changes (currently 146)
- Major features completed (add to Recent Implementations)
- New data sources added
- Critical conventions change

**Update QUICK_REFERENCE.md when**:
- New services/repositories registered in DI
- New constants added to Constants.cs
- Common patterns emerge that should be documented
- New test patterns introduced

**Update GOTCHAS.md when**:
- You make a mistake or discover an edge case
- User reports a bug caused by a common pitfall
- New validation rules added
- Database quirks discovered

**Update ARCHITECTURE.md when**:
- New projects added to solution
- Database schema changes significantly
- New data flow pipelines created
- Deployment configuration changes

### How to Update

1. **Read the file first** before editing
2. **Keep MEMORY.md under 200 lines** (currently 73) - it loads in every session
3. **Be concise** - supplement, don't duplicate CLAUDE.md
4. **Use examples** - show actual code patterns, not pseudocode
5. **Archive old content** - Move outdated "Recent Implementations" to ARCHITECTURE.md history section

### Update Frequency
- After completing any significant feature
- When you encounter a new gotcha worth documenting
- After adding 10+ new tests (update test count)
- When user asks "remember this" or "don't do X again"

## Critical Context

### Test Data (TestBase.cs)
```
Heroes: Abathur (id=1, Specialist), Arthas (id=2, Warrior/Tank), Valla (id=3, Assassin)
Details: Arthas has 2 abilities, 2 talents
Patches: 2 patches (2023-12-05, 2023-11-14)
Other: 1 patch section, 1 battleground (Cursed Hollow)
```

### Key Conventions
- **ShortName**: URL-friendly identifier ("abathur", "li-ming")
- **Build codes**: `[T1331221,heroname]` = 7 talent choices (1-5 per tier)
- **JSON columns**: TagsJson, CountersJson, AbilityLinksJson, etc. (stored as serialized strings)
- **Async pattern**: All async methods end with `Async`, take `CancellationToken cancellationToken = default` as final parameter

## Recent Implementations

### Phase 1: Heroes-Data Integration (2026-02-14)
- Integrated HeroesToolChest/heroes-data as supplementary data source
- Extended models: Hero (12 new stat fields), Ability (6 new fields), Talent (5 new fields)
- Two-phase sync: heroes-talents (primary) → heroes-data (enhancement)
- Created `HeroesDataSyncService` with graceful degradation

### Phase 2: S2MA Battleground Parsing (2026-02-14)
- Created `S2MAParserService` to parse battleground metadata from S2MA map files
- Hybrid approach: S2MA (authoritative) + Fandom wiki (fallback for descriptions)
- Reduces dependency on fragile web scraping

### Phase 3: Localization - Deferred
- Foundation complete (Language enum created)
- Full plan documented in `docs/PHASE3_LOCALIZATION_PLAN.md`

## Data Sources
1. **heroespatchnotes/heroes-talents** - Primary hero data (JSON, curated)
2. **HeroesToolChest/heroes-data** - Comprehensive hero stats (JSON, from game files)
3. **jamiephan/HeroesOfTheStorm_S2MA** - Battleground map files (authoritative)
4. **heroespatchnotes/heroes-patch-data** - Patch version tracking
5. **Fandom Wiki** - Fallback for battleground descriptions
6. **BlueTracker** - Official patch notes scraping

## Development Commands

```bash
# Build & Test
dotnet build
dotnet test

# Run (both required for local dev)
cd src/HotsPatchNotes.Api && dotnet run    # https://localhost:7001
cd src/HotsPatchNotes.Web && dotnet run    # https://localhost:7000

# Trigger initial data sync
curl -k -X POST https://localhost:7001/api/sync

# Docker
docker-compose up -d
```
