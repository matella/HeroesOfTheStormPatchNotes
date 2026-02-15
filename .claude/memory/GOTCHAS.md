# Gotchas: Common Pitfalls and Debugging

> **⚠️ Maintenance Reminder**: Update this file when you discover new pitfalls, make mistakes worth documenting, or encounter edge cases. Help future sessions avoid repeating errors!

## Common Mistakes

### 1. DbContext Lifecycle
❌ **Wrong**: `builder.Services.AddSingleton<HotsDbContext>()`
✅ **Correct**: `builder.Services.AddDbContext<HotsDbContext>()` (scoped by default)

**Why**: DbContext is not thread-safe and must be scoped to a single request.

### 2. Missing CancellationToken
❌ **Wrong**: `await repository.GetAllAsync()`
✅ **Correct**: `await repository.GetAllAsync(cancellationToken)`

**Why**: All async methods take `CancellationToken cancellationToken = default` as final parameter.

### 3. Forgetting .Include() for Navigation Properties
❌ **Wrong**: `context.Heroes.FirstOrDefaultAsync(h => h.Id == id)`
✅ **Correct**: `context.Heroes.Include(h => h.Abilities).Include(h => h.Talents).FirstOrDefaultAsync(h => h.Id == id)`

**Why**: Without `.Include()`, navigation properties will be null (lazy loading disabled). Results in N+1 queries if accessed later.

### 4. Using `git add .` Instead of Specific Files
❌ **Wrong**: `git add .`
✅ **Correct**: `git add src/HotsPatchNotes.Api/Services/HeroService.cs`

**Why**: May accidentally include sensitive files (.env, credentials) or untracked test files.

### 5. Amending Commits After Pre-Commit Hook Failures
❌ **Wrong**: `git commit --amend` (after hook failure)
✅ **Correct**: Create a NEW commit (hooks prevent commit, so --amend modifies previous commit)

**Why**: When a pre-commit hook fails, the commit did NOT happen. Using --amend will modify the previous commit, potentially destroying work.

### 6. Creating DTOs in Controller Layer
❌ **Wrong**: Create `HeroDetailDto` directly in controller
✅ **Correct**: Service layer handles DTO mapping via `MapToDetailDto()`

**Why**: Violates separation of concerns. Controllers handle HTTP, services handle business logic.

---

## Database Gotchas

### 1. JSON Column Serialization
**Issue**: JSON columns (TagsJson, CountersJson) store serialized `List<string>` as strings.

**Solution**: Use `DeserializeJsonList()` helper in service layer:
```csharp
private static List<string> DeserializeJsonList(string? json)
{
    if (string.IsNullOrWhiteSpace(json)) return [];
    try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
    catch { return []; }
}
```

### 2. SQLite vs PostgreSQL Connection Strings
**SQLite**: `Data Source=hots.db`
**PostgreSQL**: `Host=localhost;Database=hots;Username=user;Password=pass`

**Switch**: Set `DatabaseProvider` in `appsettings.json` to "SQLite" or "PostgreSQL"

### 3. In-Memory Database Isolation
**Issue**: Each GUID creates a new database instance.

**Solution**: `CreateContext()` uses `Guid.NewGuid().ToString()` to ensure test isolation. Don't reuse contexts across tests.

### 4. Materialization Required
❌ **Wrong**: `var query = context.Heroes.Where(...); return query;` (IQueryable)
✅ **Correct**: `return await query.ToListAsync(cancellationToken);`

**Why**: Query must be materialized (executed) before returning from repository.

### 5. Eager Loading with ThenInclude
**Single level**: `.Include(h => h.Abilities)`
**Nested**: `.Include(h => h.Abilities).ThenInclude(a => a.LinkedTalents)`

---

## Testing Pitfalls

### 1. Manually Seeding Test Data
❌ **Wrong**: Manually create heroes in each test
✅ **Correct**: Use `CreateContextWithData()` from `TestBase`

**Why**: `TestBase.SeedTestData()` provides consistent test data (Abathur, Arthas, Valla).

### 2. Test Data Reference
**Heroes**:
- Abathur (id=1, ShortName="abathur", Role="Specialist")
- Arthas (id=2, ShortName="arthas", Role="Warrior", ExpandedRole="Tank") - has 2 abilities, 2 talents
- Valla (id=3, ShortName="valla", Role="Assassin", Type="Ranged")

**Patches**: 2 patches (2023-12-05, 2023-11-14)
**Other**: 1 patch section (Arthas), 1 battleground (Cursed Hollow)

### 3. Moq Usage
❌ **Wrong**: Mock `IHeroRepository` when testing services
✅ **Correct**: Use real repositories + in-memory DB

**Why**: Only mock external dependencies (HttpClient, ILogger). Repositories use real EF Core with in-memory DB.

### 4. Not Disposing Context
❌ **Wrong**: `var context = CreateContext(); // no using`
✅ **Correct**: `using var context = CreateContextWithData();`

**Why**: Memory leaks if context not disposed.

### 5. Missing Arrange/Act/Assert Comments
❌ **Wrong**: No comments in test
✅ **Correct**: Explicit `// Arrange`, `// Act`, `// Assert` comments

**Why**: Code convention requires explicit test structure.

---

## Build Code Edge Cases

### 1. Format Validation
**Valid formats**:
- `[T1331221,abathur]` (full format with hero name)
- `1331221` (talent code only)

**Invalid formats**:
- `[T133122,abathur]` (6 digits, need 7)
- `[T1331226,abathur]` (digit 6 out of range, max is 5)
- `[T133122a,abathur]` (non-digit character)

### 2. Talent Code Requirements
- **Length**: Exactly 7 digits (one per tier)
- **Range**: Each digit must be '1'-'5'
- **No zero**: Digit '0' is invalid
- **Case sensitivity**: Hero name is case-insensitive ("Abathur" = "abathur")

### 3. Parsing Pattern
```csharp
[GeneratedRegex(@"\[T(\d{7}),(\w+)\]", RegexOptions.IgnoreCase)]
private static partial Regex BuildCodeRegex();
```

### 4. Validation Logic
```csharp
private static bool IsValidTalentCode(string? code)
{
    return !string.IsNullOrWhiteSpace(code) &&
           code.Length == Constants.Talents.TierCount && // 7
           code.All(c => c >= Constants.Talents.MinChoice && c <= Constants.Talents.MaxChoice); // '1'-'5'
}
```

---

## Sync Service Issues

### 1. GitHub Rate Limiting
**Limit**: 60 requests/hour (unauthenticated)
**Solution**: Multi-phase sync with graceful degradation. If heroes-data fails, heroes-talents data is still usable.

### 2. Multi-Phase Sync Failures
**Phase 1 (heroes-talents)**: Critical - sync fails if this fails
**Phase 2 (heroes-data)**: Optional - enhances existing data, graceful degradation
**Phase 3 (S2MA)**: Optional - fallback to wiki scraping

### 3. S2MA Missing Files
**Issue**: S2MA repo may not have all battleground files.

**Solution**: Fallback to Fandom wiki scraping:
```csharp
if (s2maData is null)
{
    await battlegroundScraper.ScrapeBattlegroundsAsync(cancellationToken);
}
```

### 4. Fandom Wiki DOM Changes
**Issue**: Web scraping is fragile - DOM structure may change.

**Solution**:
1. S2MA parsing is primary (authoritative data from game files)
2. Wiki scraping is fallback only
3. Use `HtmlAgilityPack` with defensive null checks

### 5. Sync Timing
**Background Service**: Runs every 6 hours
**Manual Trigger**: `POST /api/sync`
**First Run**: Trigger manually after deployment

---

## EF Core Query Patterns

### 1. String Comparison
❌ **Wrong**: `h.ShortName == shortName` (case-sensitive)
✅ **Correct**: `h.ShortName == shortName.ToLowerInvariant()` (ShortName is stored lowercase)

### 2. Null Propagation
❌ **Wrong**: `h.ExpandedRole == role` (may throw if ExpandedRole is null)
✅ **Correct**: `h.ExpandedRole == role || h.Role == role` (fallback to Role)

### 3. OrderBy Before ToListAsync
✅ **Correct**: `query.OrderBy(h => h.Name).ToListAsync(cancellationToken)`

**Why**: Ensures consistent ordering (important for tests and pagination).

---

## Response Caching

### Cache Durations
- **Static data (heroes, patches, battlegrounds)**: 3600s (1 hour)
- **Dynamic data (builds)**: 300s (5 minutes)
- **Sync endpoint**: No cache

### Attribute Usage
```csharp
[HttpGet]
[ResponseCache(Duration = 3600)]
public async Task<ActionResult<List<HeroSummaryDto>>> GetHeroesAsync(...)
```

---

## Error Handling

### Controller Pattern
```csharp
try
{
    var result = await service.DoSomethingAsync(cancellationToken);
    return Ok(result);
}
catch (Exception ex)
{
    logger.LogError(ex, "Error message with context");
    throw; // Let GlobalExceptionMiddleware handle
}
```

### Never Swallow Exceptions
❌ **Wrong**: `catch (Exception) { return null; }`
✅ **Correct**: `throw;` or `logger.LogError(ex, ...); throw;`

### Use ErrorResponseDto
```csharp
return NotFound(new ErrorResponseDto
{
    Message = Constants.ErrorMessages.HeroNotFound,
    Detail = $"No hero found with short name: {shortName}",
    StatusCode = 404
});
```
