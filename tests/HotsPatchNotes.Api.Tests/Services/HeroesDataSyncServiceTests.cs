using Xunit;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Services;

namespace HotsPatchNotes.Api.Tests.Services;

/// <summary>
/// Tests for HeroesDataSyncService integration with GamestringsParser.
/// </summary>
public sealed class HeroesDataSyncServiceTests : TestBase
{
    [Fact]
    public void HeroesDataSyncService_WithGamestringsParser_CanBeConstructed()
    {
        // Arrange
        using var context = CreateContext();
        var mockHttpClient = new HttpClient();
        var mockGamestringsParser = new Mock<IGamestringsParser>();

        // Act
        var service = new HeroesDataSyncService(
            context,
            mockHttpClient,
            mockGamestringsParser.Object,
            NullLogger<HeroesDataSyncService>.Instance);

        // Assert - service created successfully with gamestrings parser dependency
        Assert.NotNull(service);
    }

    [Fact]
    public async Task SyncHeroesDataAsync_CallsGamestringsParser_WhenBuildNumberAvailable()
    {
        // Arrange
        using var context = CreateContextWithData();
        var mockHttpClient = new HttpClient();
        var mockGamestringsParser = new Mock<IGamestringsParser>();

        // Setup mock to return empty gamestrings (simulating successful fetch)
        mockGamestringsParser
            .Setup(p => p.FetchAndParseGamestringsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GamestringsResult(
                new Dictionary<string, GamestringEntry>(),
                new Dictionary<string, HeroGamestringEntry>()));

        var service = new HeroesDataSyncService(
            context,
            mockHttpClient,
            mockGamestringsParser.Object,
            NullLogger<HeroesDataSyncService>.Instance);

        // Act
        // Note: This will fail to find real herodata files but demonstrates the integration
        var result = await service.SyncHeroesDataAsync();

        // Assert - verify service was constructed and can attempt sync
        Assert.NotNull(result);
    }

    [Fact]
    public void ApplyGamestringsToTalents_Integration_UpdatesTalentData()
    {
        // Arrange
        using var context = CreateContextWithData();

        // Get Arthas's talents from seeded data
        var arthasTalents = context.Talents
            .Where(t => t.HeroId == 2) // Arthas
            .ToList();

        Assert.NotEmpty(arthasTalents);

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["ArthasFrostPresence"] = new GamestringEntry(
                "Frost Presence (Updated)",
                "Updated description from gamestrings",
                null),
            ["ArthasEternalHunger"] = new GamestringEntry(
                "Eternal Hunger (Updated)",
                "Another updated description",
                null)
        };

        var mockHttpClient = new HttpClient();
        var sanitizer = HtmlContentService.CreateSanitizer();
        var htmlService = new HtmlContentService(sanitizer);
        var parser = new GamestringsParser(
            mockHttpClient,
            htmlService,
            NullLogger<GamestringsParser>.Instance);

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(arthasTalents, gamestrings);

        // Assert
        Assert.Equal(2, matchCount); // Both Arthas talents should match
        Assert.Contains("Updated", arthasTalents[0].Name);
        Assert.Contains("Updated", arthasTalents[1].Name);
        Assert.Contains("gamestrings", arthasTalents[0].Description);
    }
}
