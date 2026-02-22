using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Tests.Services;

/// <summary>
/// Tests for GamedataMapSyncService parsing and upsert logic.
/// Uses Mock HttpMessageHandler to simulate Gamedata repository responses.
/// </summary>
public sealed class GamedataMapSyncServiceTests : TestBase
{
    // Sample gamestrings lines for CursedHollow from the real gamestrings.txt format
    private const string CursedHollowLines =
        "UI/MapLoadingScreen/CursedHollow/Name=Cursed Hollow\n" +
        "UI/MapLoadingScreen/CursedHollow/Title1=Tribute\n" +
        "UI/MapLoadingScreen/CursedHollow/Description1=Collect Tributes to call down a Curse upon the enemy team.\n" +
        "UI/MapLoadingScreen/CursedHollow/Title2=Cursed\n" +
        "UI/MapLoadingScreen/CursedHollow/Description2=The enemy team's Forts and Keeps no longer block Minion damage.\n" +
        "UI/MapLoadingScreen/CursedHollow/Title3=Raven Lord\n" +
        "UI/MapLoadingScreen/CursedHollow/Description3=A powerful being controls the ravens and tributes.\n";

    private const string AlteracValleyLines =
        "UI/MapLoadingScreen/AlteracValley/Name=Alterac Pass\n" +
        "UI/MapLoadingScreen/AlteracValley/Title1=Knight Captain\n" +
        "UI/MapLoadingScreen/AlteracValley/Description1=Defeat the Knight Captains to call in a powerful Cavalry Charge.\n";

    private static HttpClient CreateMockHttpClient(string mainResponse, string alteracResponse = "",
        HttpStatusCode mainStatusCode = HttpStatusCode.OK,
        HttpStatusCode alteracStatusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();

        // Match main URL
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("heroesdata.stormmod")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = mainStatusCode,
                Content = new StringContent(mainResponse)
            });

        // Match Alterac Pass URL
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("alteracpass.stormmod")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = alteracStatusCode,
                Content = new StringContent(alteracResponse)
            });

        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_ValidGamestrings_CreatesNewBattleground()
    {
        // Arrange
        using var context = CreateContext();
        var httpClient = CreateMockHttpClient(CursedHollowLines);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        var result = await service.SyncBattlegroundsFromGamedataAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.HeroesUpdated);

        var bg = context.Battlegrounds.FirstOrDefault(b => b.ShortName == "cursed-hollow");
        Assert.NotNull(bg);
        Assert.Equal("Cursed Hollow", bg.Name);
        Assert.Equal("3-Lane", bg.MapType);
        Assert.Equal("Raven Lord", bg.Universe);
        Assert.Equal("Tribute", bg.Objective);
        Assert.Contains("Collect Tributes", bg.Description);
        Assert.True(bg.IsInRotation);
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_ExistingBattleground_OverwritesAuthoritativeFields()
    {
        // Arrange
        using var context = CreateContext();

        // Seed an existing battleground with outdated authoritative fields
        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "cursed-hollow",
            Name = "Old Name",
            MapType = "2-Lane",
            Universe = "OldUniverse",
            IsInRotation = false
        });
        context.SaveChanges();

        var httpClient = CreateMockHttpClient(CursedHollowLines);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        var result = await service.SyncBattlegroundsFromGamedataAsync();

        // Assert
        Assert.True(result.Success);

        var bg = context.Battlegrounds.FirstOrDefault(b => b.ShortName == "cursed-hollow");
        Assert.NotNull(bg);

        // Authoritative fields should be overwritten
        Assert.Equal("Cursed Hollow", bg.Name);
        Assert.Equal("3-Lane", bg.MapType);
        Assert.Equal("Raven Lord", bg.Universe);
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_ExistingBattleground_PreservesEnrichmentFields()
    {
        // Arrange
        using var context = CreateContext();

        // Seed an existing battleground with wiki-enriched fields already populated
        const string existingTiming = "3:00 minutes";
        const string existingTips = "Prioritize capturing tributes together";
        const string existingObjective = "Wiki-enriched objective text";
        const string existingDescription = "Wiki-enriched description text";

        context.Battlegrounds.Add(new Battleground
        {
            ShortName = "cursed-hollow",
            Name = "Cursed Hollow",
            MapType = "3-Lane",
            Universe = "Raven Lord",
            ObjectiveTiming = existingTiming,
            Tips = existingTips,
            Objective = existingObjective,
            Description = existingDescription,
            IsInRotation = true
        });
        context.SaveChanges();

        var httpClient = CreateMockHttpClient(CursedHollowLines);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        await service.SyncBattlegroundsFromGamedataAsync();

        // Assert — wiki-owned fields must not be overwritten
        var bg = context.Battlegrounds.FirstOrDefault(b => b.ShortName == "cursed-hollow");
        Assert.NotNull(bg);
        Assert.Equal(existingTiming, bg.ObjectiveTiming);
        Assert.Equal(existingTips, bg.Tips);
        Assert.Equal(existingObjective, bg.Objective);       // null-coalesced: existing preserved
        Assert.Equal(existingDescription, bg.Description);   // null-coalesced: existing preserved
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_UnknownMapKey_IsIgnored()
    {
        // Arrange
        using var context = CreateContext();

        var unknownMapLines =
            "UI/MapLoadingScreen/UnknownFantasyMap/Name=Unknown Map\n" +
            "UI/MapLoadingScreen/UnknownFantasyMap/Title1=Some Objective\n";

        var httpClient = CreateMockHttpClient(unknownMapLines);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        var result = await service.SyncBattlegroundsFromGamedataAsync();

        // Assert
        Assert.Equal(0, result.HeroesUpdated);
        Assert.Empty(context.Battlegrounds.ToList());
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_HttpFailure_ReturnsEmptyResult()
    {
        // Arrange
        using var context = CreateContext();
        var httpClient = CreateMockHttpClient(
            mainResponse: "",
            alteracResponse: "",
            mainStatusCode: HttpStatusCode.NotFound,
            alteracStatusCode: HttpStatusCode.NotFound);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        var result = await service.SyncBattlegroundsFromGamedataAsync();

        // Assert — should not throw, should return with errors or empty
        Assert.NotNull(result);
        Assert.Equal(0, context.Battlegrounds.Count());
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task SyncBattlegroundsFromGamedataAsync_AlteracPass_FetchedFromSeparateUrl()
    {
        // Arrange — main URL returns nothing useful; Alterac URL has the AlteracValley key
        using var context = CreateContext();
        var httpClient = CreateMockHttpClient(
            mainResponse: "// some unrelated line\n",
            alteracResponse: AlteracValleyLines);
        var service = CreateGamedataMapSyncService(context, httpClient);

        // Act
        var result = await service.SyncBattlegroundsFromGamedataAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.HeroesUpdated);

        var bg = context.Battlegrounds.FirstOrDefault(b => b.ShortName == "alterac-pass");
        Assert.NotNull(bg);
        Assert.Equal("Alterac Pass", bg.Name);
        Assert.Equal("3-Lane", bg.MapType);
        Assert.Equal("Warcraft", bg.Universe);
        Assert.Equal("Knight Captain", bg.Objective);
    }
}
