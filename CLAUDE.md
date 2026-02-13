# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Heroes of the Storm Patch Notes — a full-stack .NET 10.0 application that aggregates hero data, patch notes, and battleground info from GitHub repositories and web scraping. Blazor WebAssembly frontend, ASP.NET Core Web API backend, SQLite (default) or PostgreSQL database.

## Build & Run Commands

```bash
# Build entire solution
dotnet build

# Run tests (xUnit + Moq + EF Core InMemory)
dotnet test

# Run API (serves on https://localhost:7001, http://localhost:5001)
cd src/HotsPatchNotes.Api && dotnet run

# Run Web (serves on https://localhost:7000, http://localhost:5100)
cd src/HotsPatchNotes.Web && dotnet run

# Trigger initial data sync after starting API
curl -k -X POST https://localhost:7001/api/sync

# Docker
docker-compose up -d
```

Both API and Web must run simultaneously for local development.

## Architecture

```
GitHub Repos + Web Scraping → API (ASP.NET Core) → Shared Models → Web (Blazor WASM)
```

**Three projects + one test project:**

- **`src/HotsPatchNotes.Api/`** — REST API with EF Core, background sync service (6-hour interval), GitHub data fetching, web scraping (HtmlAgilityPack/HtmlSanitizer)
  - `Controllers/` → REST endpoints
  - `Services/` → Business logic (GitHubSyncService, HeroService, PatchService, BattlegroundService)
  - `Repositories/` → Data access layer (repository pattern with interfaces)
  - `Data/` → EF Core DbContext

- **`src/HotsPatchNotes.Web/`** — Blazor WebAssembly SPA (no npm/Node.js)
  - `Pages/` → Razor page components
  - `Services/` → HTTP clients calling the API
  - API base URL configured in `wwwroot/appsettings.json`

- **`src/HotsPatchNotes.Shared/`** — Domain models (`Models/`) and DTOs (`DTOs/`) shared between API and Web

- **`tests/HotsPatchNotes.Api.Tests/`** — Unit tests inheriting from `TestBase`, which provides `CreateContextWithData()` for in-memory DB seeding with 3 test heroes (Abathur, Arthas, Valla)

## Key Conventions

- **Repository + Service layer pattern** — interfaces for all repositories and services, registered via DI in `Program.cs`
- **Nullable reference types** enabled across all projects
- **ShortName** is the URL-friendly hero identifier (e.g., "abathur", "li-ming")
- **JSON-in-DB columns** — `TagsJson`, `CountersJson`, `AbilityLinksJson` store serialized `List<string>` as strings in the database
- **Build code format** — `[T1331221,heroname]` where the 7-digit string represents one talent choice per tier (levels 1/4/7/10/13/16/20)
- **API endpoints** follow REST: `GET /api/heroes`, `GET /api/heroes/{shortName}`, `POST /api/sync`, query params for filtering (`?role=Tank&type=Melee&search=term`)
- **Database provider** switchable via `appsettings.json` `DatabaseProvider` setting ("SQLite" or "PostgreSQL")

## Data Sources

- Hero data synced from `heroespatchnotes/heroes-talents` GitHub repo
- Patch data synced from `heroespatchnotes/heroes-patch-data` GitHub repo
- Battleground info scraped from Fandom wiki
- Patch notes scraped from Blizzard's BlueTracker

## Code Style

### Design Principles
Strictly follow these principles in all code:
- **SOLID** — Single responsibility, open/closed, Liskov substitution, interface segregation, dependency inversion
- **DRY** — Don't repeat yourself; extract shared logic into reusable methods or constants
- **KISS** — Keep it simple; prefer straightforward solutions over clever ones
- **YAGNI** — Don't build for hypothetical future requirements
- **Separation of Concerns** — Controllers handle HTTP, services handle business logic, repositories handle data access
- **Dependency Injection** — All dependencies injected via constructor; never use `new` for services/repos
- **Composition over Inheritance** — Favor interfaces and composition; avoid deep class hierarchies
- **Fail Fast** — Validate inputs early and return meaningful errors immediately
- **Immutability** — Prefer read-only properties and records for DTOs; minimize mutable state
- **Least Privilege** — Classes are `sealed` by default; expose only what is necessary via interfaces

### C#
- **Primary constructors** on all services, repositories, and controllers (e.g., `public sealed class HeroService(IHeroRepository heroRepository, ...) : IHeroService`)
- **`sealed`** on all concrete service/repository/controller implementations
- **`partial` classes** when using `[GeneratedRegex]` source generators
- **File-scoped namespaces** (`namespace Foo.Bar;` not block-scoped)
- **Collection expressions** for empty collections (`return [];` not `return new List<T>()`)
- **Pattern matching for null checks** (`if (hero is null)` not `if (hero == null)`)

### Async Conventions
- All async methods take `CancellationToken cancellationToken = default` as final parameter
- All async methods suffixed with `Async`
- Pass `cancellationToken` through to all EF Core and downstream calls

### API Layer
- Controllers use `ActionResult<T>` return types
- Errors returned via `ErrorResponseDto` with message from `Constants.ErrorMessages`
- Use `[ResponseCache(Duration = N)]` on GET endpoints (3600s for data, 300s for builds)
- Route pattern: `[Route("api/[controller]")]` with `[ApiController]`

### Repository Layer
- Interface + sealed implementation (e.g., `IHeroRepository` / `HeroRepository`)
- Use EF Core `.Include()` for eager loading navigation properties
- Use `.ToListAsync(cancellationToken)` for materialization
- All registered as `AddScoped<>` in DI

### DTO Mapping
- Manual mapping in service layer (no AutoMapper) — static `MapToXxxDto` methods in the service class
- JSON-stored lists deserialized via `JsonSerializer.Deserialize<List<string>>()` with empty-list fallback
- DTOs grouped by domain: `HeroDtos.cs`, `PatchDtos.cs`, `BattlegroundDtos.cs`, `CommonDtos.cs`

### Blazor / Frontend
- Pages use `@inject` for service dependencies, `@code { }` block at bottom
- Loading state pattern: `bool loading = true` → set to `false` in `finally` block of `OnInitializedAsync`
- Web services silently catch `HttpRequestException` and return empty/null (no error propagation to UI)
- Use `Constants.ApiRoutes` for base route strings

### Testing
- Test classes extend `TestBase` (provides `CreateContext()`, `CreateContextWithData()`, `CreateXxxService(context)`)
- Naming: `MethodName_Condition_ExpectedResult` (e.g., `GetHero_NonExistingHero_ReturnsNotFound`)
- Arrange/Act/Assert with explicit comments
- Real repositories + in-memory DB (not mocked repos); Moq used only for external dependencies
- Assert with xUnit: `Assert.IsType<T>`, `Assert.Equal`, `Assert.Single`, `Assert.NotEmpty`

### Constants
- All magic strings/numbers go in `Shared/Constants.cs` nested static classes
- Error messages: `Constants.ErrorMessages.XxxNotFound`
- Route prefixes: `Constants.ApiRoutes.Xxx`
- Talent constants: `Constants.Talents.TierCount`, `Constants.Talents.TierLevels`
