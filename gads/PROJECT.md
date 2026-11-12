# Project Rules

This file is project-owned. GADS creates it once and never overwrites or removes it. Put
project-specific rules here instead of editing the GADS-managed files, so upgrades stay clean.

These rules rank as repository-specific approved policy in the `AGENTS.md` precedence order. They may
tighten GADS defaults; they must not weaken organizational safety controls.

## Project overview

Heroes of the Storm Patch Notes — a full-stack .NET 10.0 application that aggregates hero data, patch notes, and battleground info from GitHub repositories and web scraping. Blazor WebAssembly frontend, ASP.NET Core Web API backend, SQLite (default) or PostgreSQL database.

```
GitHub Repos + Web Scraping → API (ASP.NET Core) → Shared Models → Web (Blazor WASM)
```

## Stack and layout

- Languages and frameworks: C# / .NET 10.0, ASP.NET Core Web API, Blazor WebAssembly (no npm/Node.js), EF Core (SQLite or PostgreSQL), xUnit + Moq + EF Core InMemory.
- Main directories:
  - `src/HotsPatchNotes.Api/` — REST API with EF Core, background sync service (6-hour interval), GitHub data fetching, web scraping (HtmlAgilityPack/HtmlSanitizer)
    - `Controllers/` → REST endpoints
    - `Services/` → Business logic (GitHubSyncService, HeroService, PatchService, BattlegroundService)
    - `Repositories/` → Data access layer (repository pattern with interfaces)
    - `Data/` → EF Core DbContext
  - `src/HotsPatchNotes.Web/` — Blazor WebAssembly SPA
    - `Pages/` → Razor page components
    - `Services/` → HTTP clients calling the API
    - API base URL configured in `wwwroot/appsettings.json`
  - `src/HotsPatchNotes.Shared/` — Domain models (`Models/`) and DTOs (`DTOs/`) shared between API and Web
  - `tests/HotsPatchNotes.Api.Tests/` — Unit tests inheriting from `TestBase`, which provides `CreateContextWithData()` for in-memory DB seeding with 3 test heroes (Abathur, Arthas, Valla)

## Commands

| Purpose | Command |
|---|---|
| Build | `dotnet build` |
| Test | `dotnet test` |
| Lint | None configured (no analyzers or `.editorconfig` rules enforced beyond the compiler) |
| Format | None configured (`dotnet format` is available but not enforced) |
| Run API | `cd src/HotsPatchNotes.Api && dotnet run` (https://localhost:7001, http://localhost:5001) |
| Run Web | `cd src/HotsPatchNotes.Web && dotnet run` (https://localhost:7000, http://localhost:5100) |
| Initial data sync | `curl -k -X POST https://localhost:7001/api/sync` (after starting the API) |
| Docker | `docker-compose up -d` |

Both API and Web must run simultaneously for local development.

## Data sources

- Hero data synced from `HeroesToolChest/heroes-data` (primary), enriched from `jamiephan/HeroesOfTheStorm_Gamedata`
- Patch data synced from `heroespatchnotes/heroes-patch-data`
- Battleground info scraped from the Fandom wiki
- Patch notes scraped from Blizzard's BlueTracker

## Conventions

### Domain

- **ShortName** is the URL-friendly hero identifier (e.g., "abathur", "li-ming")
- **JSON-in-DB columns** — `TagsJson`, `CountersJson`, `AbilityLinksJson` store serialized `List<string>` as strings in the database
- **Build code format** — `[T1331221,heroname]` where the 7-digit string represents one talent choice per tier (levels 1/4/7/10/13/16/20)
- **API endpoints** follow REST: `GET /api/heroes`, `GET /api/heroes/{shortName}`, `POST /api/sync`, query params for filtering (`?role=Tank&type=Melee&search=term`)
- **Database provider** switchable via `appsettings.json` `DatabaseProvider` setting ("SQLite" or "PostgreSQL")

### Design principles

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

### File organization

- **One class per file** — each public class, record, or struct in its own file named after the class (services, repositories, controllers, models, background workers)
- **Exception: DTOs grouped by domain** — `HeroDtos.cs`, `PatchDtos.cs`, `BattlegroundDtos.cs`, `CommonDtos.cs`
- **Exception: nested classes** within a parent class are acceptable

### Async

- All async methods take `CancellationToken cancellationToken = default` as final parameter
- All async methods suffixed with `Async`
- Pass `cancellationToken` through to all EF Core and downstream calls

### API layer

- Controllers use `ActionResult<T>` return types
- Errors returned via `ErrorResponseDto` with message from `Constants.ErrorMessages`
- Use `[ResponseCache(Duration = N)]` on GET endpoints (3600s for data, 300s for builds)
- Route pattern: `[Route("api/[controller]")]` with `[ApiController]`

### Repository layer

- Interface + sealed implementation (e.g., `IHeroRepository` / `HeroRepository`)
- Use EF Core `.Include()` for eager loading navigation properties
- Use `.ToListAsync(cancellationToken)` for materialization
- All registered as `AddScoped<>` in DI

### DTO mapping

- Manual mapping in service layer (no AutoMapper) — static `MapToXxxDto` methods in the service class
- JSON-stored lists deserialized via `JsonSerializer.Deserialize<List<string>>()` with empty-list fallback

### Blazor / frontend

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

## Additional approval requirements

- Adding a new external data source requires confirming its license permits reuse (e.g. `nexus-patch-notes.github.io` is "All Rights Reserved" and must not be scraped or copied without written permission from its author).

## Out of bounds for agents

- Editing GADS-managed files (`AGENTS.md`, `CLAUDE.md`, `gads/standard/`, `gads/agents/`, `gads/templates/`, `gads/hooks/`, `.claude/agents/gads-*`); upgrade them with the GADS installer instead.
