using Xunit;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Repositories;
using HotsPatchNotes.Shared.Models;
using HotsPatchNotes.Shared;

namespace HotsPatchNotes.Api.Tests.Repositories;

/// <summary>
/// Tests for BattlegroundRepository data access methods.
/// </summary>
public sealed class BattlegroundRepositoryTests : TestBase
{
    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsAllBattlegrounds()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("cursed-hollow", result[0].ShortName);
        Assert.Equal("Cursed Hollow", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_FilterByInRotation_ReturnsFiltered()
    {
        // Arrange - add a battleground not in rotation
        using var context = CreateContextWithData();
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "hanamura-temple",
            Name = "Hanamura Temple",
            MapType = "2-Lane",
            IsInRotation = false,
            Universe = "Overwatch"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync(inRotation: true);

        // Assert
        Assert.Single(result);
        Assert.Equal("Cursed Hollow", result[0].Name);
        Assert.True(result[0].IsInRotation);
    }

    [Fact]
    public async Task GetAllAsync_FilterByInRotationFalse_ReturnsNonRotationMaps()
    {
        // Arrange - add a battleground not in rotation
        using var context = CreateContextWithData();
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "hanamura-temple",
            Name = "Hanamura Temple",
            MapType = "2-Lane",
            IsInRotation = false,
            Universe = "Overwatch"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync(inRotation: false);

        // Assert
        Assert.Single(result);
        Assert.Equal("Hanamura Temple", result[0].Name);
        Assert.False(result[0].IsInRotation);
    }

    [Fact]
    public async Task GetAllAsync_FilterByUniverse_ReturnsFiltered()
    {
        // Arrange - add battlegrounds from different universes
        using var context = CreateContextWithData();
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "towers-of-doom",
            Name = "Towers of Doom",
            MapType = "3-Lane",
            IsInRotation = true,
            Universe = "Raven Court"
        });
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "alterac-pass",
            Name = "Alterac Pass",
            MapType = "3-Lane",
            IsInRotation = true,
            Universe = "Warcraft"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync(universe: "Warcraft");

        // Assert
        Assert.Single(result);
        Assert.Equal("Alterac Pass", result[0].Name);
        Assert.Equal("Warcraft", result[0].Universe);
    }

    [Fact]
    public async Task GetAllAsync_UniverseFilterCaseInsensitive_ReturnsMatching()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync(universe: "NEXUS");

        // Assert
        Assert.Single(result);
        Assert.Equal("Cursed Hollow", result[0].Name);
    }

    [Fact]
    public async Task GetAllAsync_OrderedByName_ReturnsCorrectOrder()
    {
        // Arrange
        using var context = CreateContextWithData();
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "alterac-pass",
            Name = "Alterac Pass",
            MapType = "3-Lane",
            IsInRotation = true,
            Universe = "Warcraft"
        });
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "battlefield-of-eternity",
            Name = "Battlefield of Eternity",
            MapType = "2-Lane",
            IsInRotation = true,
            Universe = "Diablo"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Alterac Pass", result[0].Name);
        Assert.Equal("Battlefield of Eternity", result[1].Name);
        Assert.Equal("Cursed Hollow", result[2].Name);
    }

    [Fact]
    public async Task GetByShortNameAsync_ExistingBattleground_ReturnsBattleground()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("cursed-hollow");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cursed Hollow", result.Name);
        Assert.Equal("3-Lane", result.MapType);
        Assert.Equal("Nexus", result.Universe);
    }

    [Fact]
    public async Task GetByShortNameAsync_CaseInsensitive_ReturnsBattleground()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("CURSED-HOLLOW");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cursed Hollow", result.Name);
    }

    [Fact]
    public async Task GetByShortNameAsync_NonExistingBattleground_ReturnsNull()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetByShortNameAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_ExistingBattleground_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.ExistsAsync("cursed-hollow");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.ExistsAsync("CURSED-HOLLOW");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingBattleground_ReturnsFalse()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.ExistsAsync("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_NewBattleground_SavesAndReturnsId()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new BattlegroundRepository(context);

        var newBattleground = new Battleground
        {
            ShortName = "sky-temple",
            Name = "Sky Temple",
            MapType = "3-Lane",
            Description = "Egyptian themed map",
            Objective = "Capture temples",
            IsInRotation = true,
            Universe = "Nexus"
        };

        // Act
        var result = await repository.CreateAsync(newBattleground);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0, "ID should be generated");
        Assert.Equal("Sky Temple", result.Name);

        // Verify it was saved
        var saved = await context.Battlegrounds.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal("sky-temple", saved.ShortName);
    }

    [Fact]
    public async Task UpdateAsync_ExistingBattleground_UpdatesFields()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        var battleground = await repository.GetByShortNameAsync("cursed-hollow");
        Assert.NotNull(battleground);

        // Modify fields
        battleground.Description = "Updated description";
        battleground.IsInRotation = false;

        // Act
        await repository.UpdateAsync(battleground);

        // Assert - refetch to verify changes persisted
        var updated = await repository.GetByShortNameAsync("cursed-hollow");
        Assert.NotNull(updated);
        Assert.Equal("Updated description", updated.Description);
        Assert.False(updated.IsInRotation);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_ReturnsSectionsForBattleground()
    {
        // Arrange
        using var context = CreateContextWithData();

        // Add patch sections for the battleground
        var patch = await context.Patches.FirstAsync();
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patch.Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Tribute spawn timing adjusted"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("Cursed Hollow");

        // Assert
        Assert.Single(result);
        Assert.Equal("Cursed Hollow", result[0].EntityName);
        Assert.Equal(Constants.SectionTypes.Battleground, result[0].SectionType);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_CaseInsensitive_ReturnsSections()
    {
        // Arrange
        using var context = CreateContextWithData();

        var patch = await context.Patches.FirstAsync();
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patch.Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Changes"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("CURSED HOLLOW");

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        using var context = CreateContextWithData();

        var patches = await context.Patches.ToListAsync();
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patches[0].Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Change 1"
        });
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patches[1].Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Change 2"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("Cursed Hollow", limit: 1);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_OrderedByPatchDateDescending_ReturnsCorrectOrder()
    {
        // Arrange
        using var context = CreateContextWithData();

        var patches = await context.Patches.OrderByDescending(p => p.LiveDate).ToListAsync();

        // Add sections in reverse date order
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patches[1].Id, // Older patch
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Old change"
        });
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patches[0].Id, // Newer patch
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- New change"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("Cursed Hollow");

        // Assert
        Assert.Equal(2, result.Count);
        // Most recent patch should be first
        Assert.Contains("New change", result[0].Content);
        Assert.Contains("Old change", result[1].Content);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_NonExistingBattleground_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("NonExistent");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPatchSectionsAsync_IncludesPatch_Loaded()
    {
        // Arrange
        using var context = CreateContextWithData();

        var patch = await context.Patches.FirstAsync();
        context.PatchSections.Add(new PatchSection
        {
            PatchId = patch.Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = Constants.SectionTypes.Battleground,
            EntityName = "Cursed Hollow",
            Content = "- Change"
        });
        await context.SaveChangesAsync();

        var repository = new BattlegroundRepository(context);

        // Act
        var result = await repository.GetPatchSectionsAsync("Cursed Hollow");

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Patch);
        Assert.Equal(patch.PatchName, result[0].Patch.PatchName);
    }
}
