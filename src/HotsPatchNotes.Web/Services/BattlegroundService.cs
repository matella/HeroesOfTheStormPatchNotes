using System.Net.Http.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IBattlegroundService
{
    Task<List<BattlegroundSummaryDto>> GetBattlegroundsAsync(bool? inRotation = null, string? universe = null);
    Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName);
}

public sealed class BattlegroundService(HttpClient httpClient) : IBattlegroundService
{
    private const string BaseRoute = Constants.ApiRoutes.Battlegrounds;

    public async Task<List<BattlegroundSummaryDto>> GetBattlegroundsAsync(bool? inRotation = null, string? universe = null)
    {
        try
        {
            var queryParams = new List<string>();
            if (inRotation.HasValue)
                queryParams.Add($"inRotation={inRotation.Value}");
            if (!string.IsNullOrWhiteSpace(universe))
                queryParams.Add($"universe={Uri.EscapeDataString(universe)}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return await httpClient.GetFromJsonAsync<List<BattlegroundSummaryDto>>($"{BaseRoute}{query}") ?? [];
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public async Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<BattlegroundDetailDto>($"{BaseRoute}/{shortName}");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
