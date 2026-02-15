# Quick Reference: Code Patterns

> **📝 Maintenance Reminder**: Update this file when new services are registered, new constants added, or new patterns emerge. Keep examples accurate and based on actual project code.

## Service Registration (Program.cs)

### Database Context
```csharp
builder.Services.AddDbContext<HotsDbContext>(options =>
{
    if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString ?? "Data Source=hots.db");
});
```

### Repositories (AddScoped)
```csharp
builder.Services.AddScoped<IHeroRepository, HeroRepository>();
builder.Services.AddScoped<IPatchRepository, PatchRepository>();
builder.Services.AddScoped<IBuildRepository, BuildRepository>();
builder.Services.AddScoped<IBattlegroundRepository, BattlegroundRepository>();
```

### Services (AddScoped)
```csharp
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IPatchService, PatchService>();
builder.Services.AddScoped<IBattlegroundService, BattlegroundService>();
builder.Services.AddScoped<IHtmlContentService, HtmlContentService>();
```

### HttpClient Services
```csharp
builder.Services.AddHttpClient<IImageDownloadService, ImageDownloadService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBattlegroundScraper, BattlegroundScraper>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});

builder.Services.AddHttpClient<IHeroScraper, HeroScraper>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});

builder.Services.AddHttpClient<IHeroesDataSyncService, HeroesDataSyncService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});

builder.Services.AddHttpClient<IS2MAParserService, S2MAParserService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});

builder.Services.AddHttpClient<IGitHubSyncService, GitHubSyncService>(client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "HotsPatchNotes-API/1.0");
});
```

### Hosted Services
```csharp
builder.Services.AddHostedService<BackgroundSyncService>();
```

### HTML Sanitizer (Singleton)
```csharp
builder.Services.AddSingleton(HtmlContentService.CreateSanitizer());
```

---

## DTO Mapping Pattern

### Service Method with Manual Mapping
```csharp
public async Task<HeroDetailDto?> GetHeroAsync(string shortName, CancellationToken cancellationToken = default)
{
    var hero = await heroRepository.GetByShortNameAsync(shortName, cancellationToken);
    if (hero is null)
    {
        return null;
    }

    return MapToDetailDto(hero);
}

private static HeroDetailDto MapToDetailDto(Hero hero)
{
    var dto = new HeroDetailDto
    {
        Id = hero.Id,
        ShortName = hero.ShortName,
        Name = hero.Name,
        Icon = hero.Icon,
        Role = hero.Role,
        Tags = DeserializeJsonList(hero.TagsJson),
        Counters = DeserializeJsonList(hero.CountersJson)
    };

    // Map abilities
    dto.Abilities = hero.Abilities
        .GroupBy(a => a.FormName ?? hero.Name)
        .ToDictionary(
            g => g.Key,
            g => g.Select(a => new AbilityDto
            {
                Name = a.Name,
                Description = a.Description,
                Cooldown = a.Cooldown
            }).ToList()
        );

    return dto;
}
```

### JSON Deserialization Helper
```csharp
private static List<string> DeserializeJsonList(string? json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return [];
    }

    try
    {
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
    catch
    {
        return [];
    }
}
```

---

## Controller Pattern

### Standard Controller Template
```csharp
using Microsoft.AspNetCore.Mvc;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HeroesController(IHeroService heroService, ILogger<HeroesController> logger) : ControllerBase
{
    [HttpGet("{shortName}")]
    [ResponseCache(Duration = 3600)]
    public async Task<ActionResult<HeroDetailDto>> GetHeroAsync(
        string shortName,
        CancellationToken cancellationToken = default)
    {
        var hero = await heroService.GetHeroAsync(shortName, cancellationToken);

        if (hero is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Message = Constants.ErrorMessages.HeroNotFound,
                Detail = $"No hero found with short name: {shortName}",
                StatusCode = 404
            });
        }

        return Ok(hero);
    }
}
```

### Error Handling Pattern
```csharp
[HttpGet]
[ResponseCache(Duration = 3600)]
public async Task<ActionResult<List<HeroSummaryDto>>> GetHeroesAsync(
    CancellationToken cancellationToken = default)
{
    try
    {
        var heroes = await heroService.GetHeroesAsync(cancellationToken);
        return Ok(heroes);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to retrieve heroes");
        throw; // Let global middleware handle
    }
}
```

---

## Repository Pattern

### Interface Definition
```csharp
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

public interface IHeroRepository
{
    Task<List<Hero>> GetAllAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Hero?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default);

    Task<List<string>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string shortName, CancellationToken cancellationToken = default);
}
```

### Sealed Implementation with Primary Constructor
```csharp
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Repositories;

public sealed class HeroRepository(HotsDbContext context) : IHeroRepository
{
    public async Task<Hero?> GetByShortNameAsync(string shortName, CancellationToken cancellationToken = default)
    {
        var shortNameLower = shortName.ToLowerInvariant();
        return await context.Heroes
            .Include(h => h.Abilities)
            .Include(h => h.Talents)
            .FirstOrDefaultAsync(h => h.ShortName == shortNameLower, cancellationToken);
    }

    public async Task<List<Hero>> GetAllAsync(
        string? role = null,
        string? type = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Heroes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(h => h.ExpandedRole == role || h.Role == role);
        }

        return await query
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);
    }
}
```

---

## Test Pattern

### Test Class Template
```csharp
using HotsPatchNotes.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HotsPatchNotes.Api.Tests.Controllers;

public sealed class HeroesControllerTests : TestBase
{
    [Fact]
    public async Task GetHero_ExistingHero_ReturnsOk()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HeroesController>.Instance;
        var controller = new HeroesController(service, logger);

        // Act
        var result = await controller.GetHeroAsync("arthas");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var hero = Assert.IsType<HeroDetailDto>(okResult.Value);
        Assert.Equal("Arthas", hero.Name);
    }

    [Fact]
    public async Task GetHero_NonExistingHero_ReturnsNotFound()
    {
        // Arrange
        using var context = CreateContextWithData();
        var service = CreateHeroService(context);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HeroesController>.Instance;
        var controller = new HeroesController(service, logger);

        // Act
        var result = await controller.GetHeroAsync("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }
}
```

---

## Constants Reference

### Constants.ErrorMessages
```csharp
Constants.ErrorMessages.HeroNotFound          // "Hero not found"
Constants.ErrorMessages.PatchNotFound         // "Patch not found"
Constants.ErrorMessages.BattlegroundNotFound  // "Battleground not found"
Constants.ErrorMessages.InvalidTalentCode     // "Invalid talent code"
Constants.ErrorMessages.BuildCodeRequired     // "Build code required"
Constants.ErrorMessages.InvalidBuildCodeFormat // "Invalid build code format"
Constants.ErrorMessages.DatabaseUnavailable   // "Database is temporarily unavailable"
Constants.ErrorMessages.InternalServerError   // "An unexpected error occurred"
Constants.ErrorMessages.RequestCancelled      // "Request was cancelled"
```

### Constants.ApiRoutes
```csharp
Constants.ApiRoutes.Heroes        // "api/heroes"
Constants.ApiRoutes.Patches       // "api/patches"
Constants.ApiRoutes.Battlegrounds // "api/battlegrounds"
Constants.ApiRoutes.Admin         // "api/admin"
```

### Constants.Talents
```csharp
Constants.Talents.TierCount    // 7 (number of talent tiers)
Constants.Talents.MinChoice    // '1' (minimum talent choice)
Constants.Talents.MaxChoice    // '5' (maximum talent choice)
Constants.Talents.TierLevels   // [1, 4, 7, 10, 13, 16, 20]
```

### Constants.Pagination
```csharp
Constants.Pagination.DefaultPage     // 1
Constants.Pagination.DefaultPageSize // 20
Constants.Pagination.MaxPageSize     // 100
```

### Constants.SectionTypes
```csharp
Constants.SectionTypes.Hero        // "Hero"
Constants.SectionTypes.Battleground // "Battleground"
Constants.SectionTypes.General     // "General"
```
