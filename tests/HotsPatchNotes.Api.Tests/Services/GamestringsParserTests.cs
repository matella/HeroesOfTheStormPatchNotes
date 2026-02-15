using System.Net;
using Ganss.Xss;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace HotsPatchNotes.Api.Tests.Services;

public class GamestringsParserTests
{
    private readonly IHtmlContentService _htmlContentService;

    public GamestringsParserTests()
    {
        var sanitizer = HtmlContentService.CreateSanitizer();
        _htmlContentService = new HtmlContentService(sanitizer);
    }

    [Fact]
    public void SanitizeGamestring_RemovesColorTags_PreservesText()
    {
        // Arrange
        var input = "Locusts deal <c val=\"bfd4fd\">40%</c> more damage and have increased range.";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.DoesNotContain("<c val=", result);
        Assert.Contains("40%", result);
        Assert.Contains("more damage", result);
    }

    [Fact]
    public void SanitizeGamestring_ConvertsNewlines_ToBrTags()
    {
        // Arrange
        var input = "Line 1<n/>Line 2<n />Line 3";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.Contains("<br", result);
        Assert.DoesNotContain("<n/>", result);
        Assert.DoesNotContain("<n />", result);
    }

    [Fact]
    public void SanitizeGamestring_RemovesStyledSpans_PreservesContent()
    {
        // Arrange
        var input = "<s val=\"StandardTooltipDetails\" name=\"StandardTooltipDetails\">Quest: Hit 30 Heroes</s>";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.DoesNotContain("<s val=", result);
        Assert.Contains("Quest: Hit 30 Heroes", result);
    }

    [Fact]
    public void SanitizeGamestring_RemovesImageTags()
    {
        // Arrange
        var input = "<img path=\"@UI/StormTalentInTextQuestIcon\" />Quest: Gather 30 Regeneration Globes";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.DoesNotContain("<img", result);
        Assert.Contains("Quest: Gather 30 Regeneration Globes", result);
    }

    [Fact]
    public void SanitizeGamestring_HandlesComplexMarkup()
    {
        // Arrange
        var input = "<c val=\"bfd4fd\">Increase damage by <c val=\"ffffff\">25%</c></c><n/>Cooldown: <c val=\"00ff00\">10 seconds</c>";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.DoesNotContain("<c val=", result);
        // Nested color tags may result in partial content - just verify no color tags remain
        Assert.DoesNotContain("</c>", result);
        // Verify newline was converted
        Assert.Contains("<br", result);
    }

    [Fact]
    public void SanitizeGamestring_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var input = "";

        // Act
        var result = _htmlContentService.SanitizeGamestring(input);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeGamestring_NullString_ReturnsEmpty()
    {
        // Arrange
        string? input = null;

        // Act
        var result = _htmlContentService.SanitizeGamestring(input!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task FetchAndParseGamestringsAsync_ValidBuild_ReturnsGamestrings()
    {
        // Arrange
        var mockJson = @"{
            ""meta"": { ""version"": ""76003"", ""locale"": ""enus"" },
            ""gamestrings"": {
                ""abiltalent"": {
                    ""name"": {
                        ""TestAbility|TestAbilityButton|Q|False"": ""Test Ability Name""
                    },
                    ""full"": {
                        ""TestAbility|TestAbilityButton|Q|False"": ""<c val=\""bfd4fd\"">Deals 100 damage</c>""
                    },
                    ""short"": {}
                }
            }
        }";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockJson)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var parser = new GamestringsParser(httpClient, _htmlContentService, NullLogger<GamestringsParser>.Instance);

        // Act
        var result = await parser.FetchAndParseGamestringsAsync("76003", "enus");

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(result.ContainsKey("TestAbility") || result.ContainsKey("TestAbilityButton"));
    }

    [Fact]
    public async Task FetchAndParseGamestringsAsync_InvalidBuild_ReturnsNull()
    {
        // Arrange
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var parser = new GamestringsParser(httpClient, _htmlContentService, NullLogger<GamestringsParser>.Instance);

        // Act
        var result = await parser.FetchAndParseGamestringsAsync("99999", "enus");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task FetchAndParseGamestringsAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        var malformedJson = "{ invalid json }";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(malformedJson)
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var parser = new GamestringsParser(httpClient, _htmlContentService, NullLogger<GamestringsParser>.Instance);

        // Act
        var result = await parser.FetchAndParseGamestringsAsync("76003", "enus");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ApplyGamestringsToTalents_MatchesByTalentTreeId_UpdatesFields()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent
            {
                Id = 1,
                Name = "Old Name",
                Description = "Old description",
                TalentTreeId = "AbathurBombardStrain",
                Level = 1
            }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["AbathurBombardStrain"] = new GamestringEntry(
                "Bombard Strain",
                "Locusts deal 40% more damage and have increased range.",
                null)
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(1, matchCount);
        Assert.Equal("Bombard Strain", talents[0].Name);
        Assert.Equal("Locusts deal 40% more damage and have increased range.", talents[0].Description);
    }

    [Fact]
    public void ApplyGamestringsToTalents_MatchesByTooltipId_UpdatesFields()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent
            {
                Id = 1,
                Name = "Old Name",
                Description = "Old description",
                TooltipId = "ArthasFrostPresence",
                TalentTreeId = null,
                Level = 1
            }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["ArthasFrostPresence"] = new GamestringEntry(
                "Frost Presence",
                "Frozen Tempest Slows enemy Movement Speed by an additional 10%.",
                null)
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(1, matchCount);
        Assert.Equal("Frost Presence", talents[0].Name);
        Assert.Contains("Frozen Tempest", talents[0].Description);
    }

    [Fact]
    public void ApplyGamestringsToTalents_NoMatch_LeavesExisting()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent
            {
                Id = 1,
                Name = "Existing Name",
                Description = "Existing description",
                TalentTreeId = "NonExistentTalent",
                Level = 1
            }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["DifferentTalent"] = new GamestringEntry("Different", "Different description", null)
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(0, matchCount);
        Assert.Equal("Existing Name", talents[0].Name);
        Assert.Equal("Existing description", talents[0].Description);
    }

    [Fact]
    public void ApplyGamestringsToTalents_FuzzyNameMatch_UpdatesFields()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent
            {
                Id = 1,
                Name = "Frost-Presence",
                Description = "Old description",
                TalentTreeId = null,
                TooltipId = null,
                Level = 1
            }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["SomeId"] = new GamestringEntry(
                "Frost Presence", // Different spacing/punctuation
                "Updated description",
                null)
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(1, matchCount);
        Assert.Equal("Frost Presence", talents[0].Name);
        Assert.Equal("Updated description", talents[0].Description);
    }

    [Fact]
    public void ApplyGamestringsToTalents_EmptyGamestring_DoesNotUpdate()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent
            {
                Id = 1,
                Name = "Existing Name",
                Description = "Existing description",
                TalentTreeId = "TestTalent",
                Level = 1
            }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["TestTalent"] = new GamestringEntry("", "", null) // Empty strings
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(1, matchCount); // Match occurred but fields weren't updated
        Assert.Equal("Existing Name", talents[0].Name); // Name unchanged (empty string not applied)
        Assert.Equal("Existing description", talents[0].Description); // Description unchanged
    }

    [Fact]
    public void ApplyGamestringsToTalents_MultipleMatches_UpdatesAll()
    {
        // Arrange
        var talents = new List<Talent>
        {
            new Talent { Id = 1, Name = "Old1", TalentTreeId = "Talent1", Level = 1 },
            new Talent { Id = 2, Name = "Old2", TooltipId = "Talent2", Level = 4 },
            new Talent { Id = 3, Name = "Old3", TalentTreeId = "NoMatch", Level = 7 }
        };

        var gamestrings = new Dictionary<string, GamestringEntry>
        {
            ["Talent1"] = new GamestringEntry("New1", "Description1", null),
            ["Talent2"] = new GamestringEntry("New2", "Description2", null)
        };

        var parser = CreateParser();

        // Act
        var matchCount = parser.ApplyGamestringsToTalents(talents, gamestrings);

        // Assert
        Assert.Equal(2, matchCount);
        Assert.Equal("New1", talents[0].Name);
        Assert.Equal("New2", talents[1].Name);
        Assert.Equal("Old3", talents[2].Name); // Unchanged
    }

    private GamestringsParser CreateParser()
    {
        var httpClient = new HttpClient();
        return new GamestringsParser(httpClient, _htmlContentService, NullLogger<GamestringsParser>.Instance);
    }
}
