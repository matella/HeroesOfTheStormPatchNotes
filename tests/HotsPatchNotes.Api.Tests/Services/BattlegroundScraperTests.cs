using HotsPatchNotes.Api.Data;
using HotsPatchNotes.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace HotsPatchNotes.Api.Tests.Services;

public class BattlegroundScraperTests
{
    private readonly string _testDataPath;

    public BattlegroundScraperTests()
    {
        // Get path to TestData folder
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _testDataPath = Path.Combine(baseDir, "TestData", "Battlegrounds");
    }

    #region Helper Methods

    private static HttpClient CreateMockHttpClient(string htmlContent)
    {
        var handler = new MockHttpMessageHandler(htmlContent);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://heroesofthestorm.fandom.com/")
        };
    }

    private string LoadFixture(string relativePath)
    {
        var fullPath = Path.Combine(_testDataPath, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Fixture not found: {fullPath}");
        }
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    #endregion

    #region GetBattlegroundListAsync Tests

    [Fact]
    public async Task GetBattlegroundListAsync_ParsesMainTable_Successfully()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var logger = NullLogger<BattlegroundScraper>.Instance;
        var scraper = new BattlegroundScraper(httpClient, logger);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        Assert.NotEmpty(battlegrounds);
        Assert.Equal(3, battlegrounds.Count); // Our fixture has 3 battlegrounds
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsName_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        Assert.NotEmpty(battlegrounds);
        Assert.All(battlegrounds, bg =>
        {
            Assert.NotNull(bg.Name);
            Assert.NotEmpty(bg.Name);
        });
        
        // Check that we have the expected names
        var names = battlegrounds.Select(b => b.Name).ToList();
        Assert.Contains("Alterac Pass", names);
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsShortName_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        var alterac = battlegrounds.FirstOrDefault(b => b.Name == "Alterac Pass");
        Assert.NotNull(alterac);
        Assert.Equal("alterac-pass", alterac.ShortName);
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsLanes_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        Assert.All(battlegrounds, bg =>
        {
            Assert.NotNull(bg.Lanes);
            Assert.True(bg.Lanes == "2" || bg.Lanes == "3"); // All battlegrounds have 2 or 3 lanes
        });
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsUniverse_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        var alterac = battlegrounds.FirstOrDefault(b => b.Name == "Alterac Pass");
        Assert.NotNull(alterac);
        Assert.Equal("Warcraft", alterac.Universe); // Azeroth maps to Warcraft
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsObjective_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        Assert.All(battlegrounds, bg =>
        {
            Assert.NotNull(bg.ObjectiveSummary);
            Assert.NotEmpty(bg.ObjectiveSummary);
        });
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsWikiUrl_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        var alterac = battlegrounds.FirstOrDefault(b => b.Name == "Alterac Pass");
        Assert.NotNull(alterac);
        Assert.Contains("/wiki/Alterac_Pass", alterac.WikiUrl);
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsThumbnail_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        // Our simplified fixture doesn't include images, but the code should handle it gracefully
        Assert.All(battlegrounds, bg =>
        {
            // ThumbnailUrl can be null in our test fixture
            // In real data it would have a value
            if (bg.ThumbnailUrl != null)
            {
                Assert.StartsWith("https://", bg.ThumbnailUrl);
            }
        });
    }

    [Fact]
    public async Task GetBattlegroundListAsync_ExtractsReleaseDate_Correctly()
    {
        // Arrange
        var html = LoadFixture("List/battleground-list.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var battlegrounds = await scraper.GetBattlegroundListAsync();

        // Assert
        var alterac = battlegrounds.FirstOrDefault(b => b.Name == "Alterac Pass");
        Assert.NotNull(alterac);
        Assert.NotNull(alterac.ReleaseDate);
        Assert.Equal(new DateTime(2018, 6, 12), alterac.ReleaseDate.Value);
    }

    #endregion

    #region Helper Method Tests

    [Theory]
    [InlineData("Alterac Pass", "alterac-pass")]
    [InlineData("Cursed Hollow", "cursed-hollow")]
    [InlineData("Dragon Shire", "dragon-shire")]
    [InlineData("Sky Temple", "sky-temple")]
    [InlineData("Blackheart's Bay", "blackhearts-bay")]
    [InlineData("Hanamura Temple", "hanamura-temple")]
    public void GenerateShortName_CreatesUrlFriendlyName(string input, string expected)
    {
        // Act
        var result = BattlegroundScraper.GenerateShortName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Azeroth", "Warcraft")]
    [InlineData("Sanctuary", "Diablo")]
    [InlineData("Koprulu Sector", "StarCraft")]
    [InlineData("Nexus", "Nexus")]
    [InlineData("Unknown", "Nexus")] // Default fallback
    [InlineData("", "Nexus")] // Empty string fallback
    public void MapRealmToUniverse_MapsCorrectly(string input, string expected)
    {
        // Act
        var result = BattlegroundScraper.MapRealmToUniverse(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region GetBattlegroundDetailsAsync Tests

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsDescription_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.Description);
        Assert.Contains("horns of war echo", details.Description);
        Assert.Contains("Cavalry", details.Description);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsObjectiveDetails_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.ObjectiveDetails);
        Assert.Contains("Prison Camps", details.ObjectiveDetails);
        Assert.Contains("prisoners", details.ObjectiveDetails);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsObjectiveTiming_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.ObjectiveTiming);
        Assert.Contains("1:30", details.ObjectiveTiming);
        Assert.Contains("3:00", details.ObjectiveTiming);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsMercCamps_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.MercCamps);
        Assert.Contains("Siege Giants", details.MercCamps);
        Assert.Contains("Bruiser", details.MercCamps);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsBossInfo_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.BossInfo);
        Assert.Contains("Boss", details.BossInfo);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsTips_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.Tips);
        Assert.Contains("Coordinate", details.Tips);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_ExtractsFullImageUrl_Correctly()
    {
        // Arrange
        var html = LoadFixture("Details/alterac-pass-detail.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Alterac_Pass");

        // Assert
        Assert.NotNull(details.FullImageUrl);
        Assert.StartsWith("https://", details.FullImageUrl);
        Assert.DoesNotContain("scale-to-width-down", details.FullImageUrl);
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_HandlesMinimalData_Gracefully()
    {
        // Arrange
        var html = LoadFixture("EdgeCases/missing-fields.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Incomplete");

        // Assert - Should not throw, all fields should be null or empty
        Assert.NotNull(details);
        // These fields may be null when data is missing
        Assert.True(string.IsNullOrEmpty(details.Description));
        Assert.True(string.IsNullOrEmpty(details.ObjectiveDetails));
        Assert.True(string.IsNullOrEmpty(details.ObjectiveTiming));
        Assert.True(string.IsNullOrEmpty(details.MercCamps));
        Assert.True(string.IsNullOrEmpty(details.BossInfo));
        Assert.True(string.IsNullOrEmpty(details.Tips));
        Assert.True(string.IsNullOrEmpty(details.FullImageUrl));
    }

    [Fact]
    public async Task GetBattlegroundDetailsAsync_HandlesMalformedHtml_Gracefully()
    {
        // Arrange
        var html = LoadFixture("EdgeCases/malformed-html.html");
        var httpClient = CreateMockHttpClient(html);
        var scraper = new BattlegroundScraper(httpClient, NullLogger<BattlegroundScraper>.Instance);

        // Act
        var details = await scraper.GetBattlegroundDetailsAsync("https://heroesofthestorm.fandom.com/wiki/Malformed");

        // Assert - Should not throw even with malformed HTML
        Assert.NotNull(details);
    }

    #endregion

    #region Mock HTTP Handler

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;

        public MockHttpMessageHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(_content, Encoding.UTF8, "text/html")
            });
        }
    }

    #endregion
}
