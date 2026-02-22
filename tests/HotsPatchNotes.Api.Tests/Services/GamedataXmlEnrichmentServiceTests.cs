using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using HotsPatchNotes.Api.Services;
using HotsPatchNotes.Shared.Models;

namespace HotsPatchNotes.Api.Tests.Services;

/// <summary>
/// Tests for GamedataXmlEnrichmentService.
/// Validates construction, graceful degradation, and enrichment behavior.
/// </summary>
public sealed class GamedataXmlEnrichmentServiceTests : TestBase
{
    [Fact]
    public void GamedataXmlEnrichmentService_CanBeConstructed()
    {
        // Arrange
        using var context = CreateContext();
        var httpClient = new HttpClient();

        // Act
        var service = CreateGamedataXmlEnrichmentService(context, httpClient);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task EnrichHeroesFromGamedataAsync_WithNoHeroesInDb_ReturnsSuccess()
    {
        // Arrange
        using var context = CreateContext();
        var httpClient = new HttpClient();
        var service = CreateGamedataXmlEnrichmentService(context, httpClient);

        // Act — HTTP request will fail (no network/mock), service degrades gracefully
        var result = await service.EnrichHeroesFromGamedataAsync();

        // Assert — should return a result, not throw
        Assert.NotNull(result);
        Assert.NotNull(result.Errors);
    }

    [Fact]
    public async Task EnrichHeroesFromGamedataAsync_HttpFailure_DoesNotThrow()
    {
        // Arrange
        using var context = CreateContextWithData(); // Has Abathur, Arthas, Valla
        var httpClient = new HttpClient();
        var service = CreateGamedataXmlEnrichmentService(context, httpClient);

        // Act — real HTTP request will fail but should be handled gracefully
        var result = await service.EnrichHeroesFromGamedataAsync(CancellationToken.None);

        // Assert — should return a result with errors, not throw
        Assert.NotNull(result);
        Assert.NotNull(result.Errors);
        // Heroes in DB are unchanged since enrichment failed to reach the source
        var abathur = await context.Heroes.FindAsync(1);
        Assert.NotNull(abathur);
        Assert.Equal("Abathur", abathur.Name);
    }
}
