using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IBattlegroundService
{
    Task<List<BattlegroundSummaryDto>> GetBattlegroundsAsync(bool? inRotation = null, string? universe = null);
    Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName);
}

public sealed class BattlegroundService(HttpClient httpClient, IErrorStateService errorState) : IBattlegroundService
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
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is not null && ex.StatusCode != HttpStatusCode.NotFound)
            {
                errorState.SetError(
                    "Unable to load battlegrounds",
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable
                        ? "The server is temporarily unavailable. Please try again later."
                        : "Network connection failed. Check your connection.",
                    ErrorSeverity.Error);
            }
            return [];
        }
        catch (JsonException)
        {
            errorState.SetError(
                "Data format error",
                "Received invalid data from server. Please refresh the page.",
                ErrorSeverity.Warning);
            return [];
        }
    }

    public async Task<BattlegroundDetailDto?> GetBattlegroundAsync(string shortName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<BattlegroundDetailDto>($"{BaseRoute}/{shortName}");
        }
        catch (HttpRequestException ex)
        {
            // Don't set error for 404s (expected for non-existent battlegrounds)
            if (ex.StatusCode is not null && ex.StatusCode != HttpStatusCode.NotFound)
            {
                errorState.SetError(
                    "Unable to load battleground details",
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable
                        ? "The server is temporarily unavailable. Please try again later."
                        : "Network connection failed. Check your connection.",
                    ErrorSeverity.Error);
            }
            return null;
        }
        catch (JsonException)
        {
            errorState.SetError(
                "Data format error",
                "Received invalid data from server. Please refresh the page.",
                ErrorSeverity.Warning);
            return null;
        }
    }
}
