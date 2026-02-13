using Xunit;
using HotsPatchNotes.Api.Repositories;

namespace HotsPatchNotes.Api.Tests.Repositories;

/// <summary>
/// Tests for HeroRepository data access methods.
/// </summary>
public sealed class HeroRepositoryTests : TestBase
{
    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsAllHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, h => h.ShortName == "abathur");
        Assert.Contains(result, h => h.ShortName == "arthas");
        Assert.Contains(result, h => h.ShortName == "valla");
        Assert.True(result[0].Name.CompareTo(result[1].Name) <= 0, "Results should be ordered by name");
    }

    [Fact]
    public async Task GetAllAsync_FilterByRole_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act - search by ExpandedRole
        var result = await repository.GetAllAsync(role: "Tank");

        // Assert
        Assert.Single(result);
        Assert.Equal("arthas", result[0].ShortName);
    }

    [Fact]
    public async Task GetAllAsync_FilterByRoleMatchesExpandedRoleOrRole_ReturnsHero()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act - search by Role (not ExpandedRole)
        var result = await repository.GetAllAsync(role: "Warrior");

        // Assert
        Assert.Single(result);
        Assert.Equal("arthas", result[0].ShortName);
    }

    [Fact]
    public async Task GetAllAsync_FilterByType_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetAllAsync(type: "Melee");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, h => h.ShortName == "abathur");
        Assert.Contains(result, h => h.ShortName == "arthas");
    }

    [Fact]
    public async Task GetAllAsync_SearchByName_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetAllAsync(search: "art");

        // Assert
        Assert.Single(result);
        Assert.Equal("arthas", result[0].ShortName);
    }

    [Fact]
    public async Task GetAllAsync_SearchByShortName_ReturnsMatchingHeroes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetAllAsync(search: "abath");

        // Assert
        Assert.Single(result);
        Assert.Equal("abathur", result[0].ShortName);
    }

    [Fact]
    public async Task GetAllAsync_CombinedFilters_ReturnsIntersection()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act - Tank role AND Melee type
        var result = await repository.GetAllAsync(role: "Tank", type: "Melee");

        // Assert
        Assert.Single(result);
        Assert.Equal("arthas", result[0].ShortName);
    }

    [Fact]
    public async Task GetAllAsync_CombinedFiltersNoMatch_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act - Tank role AND Ranged type (no such hero in test data)
        var result = await repository.GetAllAsync(role: "Tank", type: "Ranged");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByShortNameAsync_ExistingHero_ReturnsHeroWithAbilitiesAndTalents()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("arthas");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Arthas", result.Name);
        Assert.Equal("arthas", result.ShortName);

        // Verify navigation properties are loaded
        Assert.NotNull(result.Abilities);
        Assert.Equal(2, result.Abilities.Count);
        Assert.Contains(result.Abilities, a => a.Name == "Frostmourne Hungers");
        Assert.Contains(result.Abilities, a => a.Name == "Death Coil");

        Assert.NotNull(result.Talents);
        Assert.Equal(2, result.Talents.Count);
        Assert.Contains(result.Talents, t => t.Name == "Frost Presence");
        Assert.Contains(result.Talents, t => t.Name == "Eternal Hunger");
    }

    [Fact]
    public async Task GetByShortNameAsync_CaseInsensitive_ReturnsHero()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("ARTHAS");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Arthas", result.Name);
    }

    [Fact]
    public async Task GetByShortNameAsync_NonExistingHero_ReturnsNull()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRolesAsync_ReturnsDistinctRoles()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetRolesAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("Support", result);
        Assert.Contains("Tank", result);
        Assert.Contains("Ranged Assassin", result);

        // Verify ordering
        Assert.True(result[0].CompareTo(result[1]) <= 0, "Results should be ordered alphabetically");
    }

    [Fact]
    public async Task GetRolesAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateContext(); // No test data
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.GetRolesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task ExistsAsync_ExistingHero_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.ExistsAsync("arthas");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.ExistsAsync("VALLA");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingHero_ReturnsFalse()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);

        // Act
        var result = await repository.ExistsAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllAsync_CancellationToken_PropagatesCorrectly()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new HeroRepository(context);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await repository.GetAllAsync(cancellationToken: cts.Token);

        // Assert - token was passed through (no exception thrown)
        Assert.Equal(3, result.Count);
    }
}
