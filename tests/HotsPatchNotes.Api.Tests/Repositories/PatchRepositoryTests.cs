using Xunit;
using Microsoft.EntityFrameworkCore;
using HotsPatchNotes.Api.Repositories;

namespace HotsPatchNotes.Api.Tests.Repositories;

/// <summary>
/// Tests for PatchRepository data access methods.
/// </summary>
public sealed class PatchRepositoryTests : TestBase
{
    [Fact]
    public async Task GetAllAsync_NoFilters_ReturnsPagedPatches()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var (items, totalCount) = await repository.GetAllAsync();

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);

        // Verify ordering (most recent first)
        Assert.Equal("2023-12-05", items[0].InternalId);
        Assert.Equal("2023-11-14", items[1].InternalId);
    }

    [Fact]
    public async Task GetAllAsync_FilterByPatchType_ReturnsMatching()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var (items, totalCount) = await repository.GetAllAsync(patchType: "Balance Update");

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(items);
        Assert.Equal("December 5, 2023 Patch", items[0].PatchName);
    }

    [Fact]
    public async Task GetAllAsync_FilterBySource_ReturnsMatching()
    {
        // Arrange - add a patch with explicit source
        using var context = CreateContextWithData();
        var patches = await context.Patches.ToListAsync();
        patches[0].Source = "github";
        patches[1].Source = "bluetracker";
        await context.SaveChangesAsync();

        var repository = new PatchRepository(context);

        // Act
        var (items, totalCount) = await repository.GetAllAsync(source: "github");

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(items);
        Assert.Equal("github", items[0].Source);
    }

    [Fact]
    public async Task GetAllAsync_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act - page 1, pageSize 1
        var (items, totalCount) = await repository.GetAllAsync(page: 1, pageSize: 1);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Single(items);
        Assert.Equal("2023-12-05", items[0].InternalId);

        // Act - page 2, pageSize 1
        var (items2, totalCount2) = await repository.GetAllAsync(page: 2, pageSize: 1);

        // Assert
        Assert.Equal(2, totalCount2);
        Assert.Single(items2);
        Assert.Equal("2023-11-14", items2[0].InternalId);
    }

    [Fact]
    public async Task GetAllAsync_EmptyPage_ReturnsEmptyList()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act - page 10 (beyond available data)
        var (items, totalCount) = await repository.GetAllAsync(page: 10, pageSize: 20);

        // Assert
        Assert.Equal(2, totalCount); // Total count stays the same
        Assert.Empty(items); // But no items on this page
    }

    [Fact]
    public async Task GetByInternalIdAsync_ExistingPatch_ReturnsPatchWithSections()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetByInternalIdAsync("2023-12-05");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("December 5, 2023 Patch", result.PatchName);
        Assert.Equal("Balance Update", result.PatchType);

        // Verify sections are loaded
        Assert.NotNull(result.Sections);
        Assert.Single(result.Sections);

        var section = result.Sections.First();
        Assert.Equal("Arthas", section.EntityName);

        // Verify sections are ordered
        Assert.Equal(1, section.Order);
    }

    [Fact]
    public async Task GetByInternalIdAsync_SectionsIncludeHero_Loaded()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetByInternalIdAsync("2023-12-05");

        // Assert
        Assert.NotNull(result);
        var section = result.Sections.First();
        Assert.NotNull(section.Hero);
        Assert.Equal("Arthas", section.Hero.Name);
    }

    [Fact]
    public async Task GetByInternalIdAsync_NonExistingPatch_ReturnsNull()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetByInternalIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPatchTypesAsync_ReturnsDistinctTypes()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetPatchTypesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("Balance Update", result);
        Assert.Contains("Hotfix", result);

        // Verify ordering
        Assert.True(result[0].CompareTo(result[1]) <= 0, "Results should be ordered alphabetically");
    }

    [Fact]
    public async Task GetPatchTypesAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateContext(); // No test data
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetPatchTypesAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSourcesAsync_ReturnsDistinctSources()
    {
        // Arrange
        using var context = CreateContextWithData();
        var patches = await context.Patches.ToListAsync();
        patches[0].Source = "github";
        patches[1].Source = "bluetracker";
        await context.SaveChangesAsync();

        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSourcesAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("github", result);
        Assert.Contains("bluetracker", result);
    }

    [Fact]
    public async Task GetSectionsAsync_NoFilters_ReturnsSectionsForPatch()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsAsync("2023-12-05");

        // Assert
        Assert.Single(result);
        Assert.Equal("Arthas", result[0].EntityName);
    }

    [Fact]
    public async Task GetSectionsAsync_FilterBySectionType_ReturnsMatching()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsAsync("2023-12-05", sectionType: "Hero");

        // Assert
        Assert.Single(result);
        Assert.Equal("Hero", result[0].SectionType);
    }

    [Fact]
    public async Task GetSectionsAsync_FilterByEntityName_ReturnsMatching()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsAsync("2023-12-05", entityName: "Arth");

        // Assert
        Assert.Single(result);
        Assert.Contains("Arthas", result[0].EntityName);
    }

    [Fact]
    public async Task GetSectionsAsync_IncludesHeroAndPatch_Loaded()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsAsync("2023-12-05");

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Hero);
        Assert.Equal("Arthas", result[0].Hero.Name);
        Assert.NotNull(result[0].Patch);
        Assert.Equal("December 5, 2023 Patch", result[0].Patch.PatchName);
    }

    [Fact]
    public async Task GetSectionsForHeroAsync_ByHeroId_ReturnsSections()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsForHeroAsync(heroId: 2, heroName: "Arthas");

        // Assert
        Assert.Single(result);
        Assert.Equal("Arthas", result[0].EntityName);
        Assert.Equal(2, result[0].HeroId);
    }

    [Fact]
    public async Task GetSectionsForHeroAsync_ByHeroName_ReturnsSections()
    {
        // Arrange - create a section with EntityName but no HeroId
        using var context = CreateContextWithData();
        var patch = await context.Patches.FirstAsync(p => p.InternalId == "2023-11-14");
        context.PatchSections.Add(new HotsPatchNotes.Shared.Models.PatchSection
        {
            PatchId = patch.Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = "Hero",
            EntityName = "Valla",
            HeroId = null, // No explicit ID, should match by name
            Content = "- Basic Attack damage increased"
        });
        await context.SaveChangesAsync();

        var repository = new PatchRepository(context);

        // Act - search by name only (heroId 999 doesn't exist)
        var result = await repository.GetSectionsForHeroAsync(heroId: 999, heroName: "Valla");

        // Assert
        Assert.Single(result);
        Assert.Equal("Valla", result[0].EntityName);
    }

    [Fact]
    public async Task GetSectionsForHeroAsync_CaseInsensitive_ReturnsMatching()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsForHeroAsync(heroId: 2, heroName: "ARTHAS");

        // Assert
        Assert.Single(result);
        Assert.Equal("Arthas", result[0].EntityName);
    }

    [Fact]
    public async Task GetSectionsForHeroAsync_OrderedByPatchDateDescending_ReturnsCorrectOrder()
    {
        // Arrange - create multiple sections across different patches
        using var context = CreateContextWithData();
        var patch2 = await context.Patches.FirstAsync(p => p.InternalId == "2023-11-14");
        context.PatchSections.Add(new HotsPatchNotes.Shared.Models.PatchSection
        {
            PatchId = patch2.Id,
            Order = 1,
            HeadingLevel = 2,
            SectionType = "Hero",
            EntityName = "Arthas",
            HeroId = 2,
            Content = "- Death Coil cooldown increased"
        });
        await context.SaveChangesAsync();

        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsForHeroAsync(heroId: 2, heroName: "Arthas");

        // Assert
        Assert.Equal(2, result.Count);

        // Verify order: most recent patch first (2023-12-05 > 2023-11-14)
        Assert.NotNull(result[0].Patch);
        Assert.Equal("2023-12-05", result[0].Patch.InternalId);
        Assert.Equal("2023-11-14", result[1].Patch.InternalId);
    }

    [Fact]
    public async Task GetSectionsForHeroAsync_NonExistingHero_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateContextWithData();
        var repository = new PatchRepository(context);

        // Act
        var result = await repository.GetSectionsForHeroAsync(heroId: 999, heroName: "NonExistent");

        // Assert
        Assert.Empty(result);
    }
}
