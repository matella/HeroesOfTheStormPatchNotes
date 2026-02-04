using System.Net.Http.Json;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IHeroService
{
    Task<List<HeroSummaryDto>> GetHeroesAsync(string? role = null, string? type = null, string? search = null);
    Task<HeroDetailDto?> GetHeroAsync(string shortName);
    Task<List<string>> GetRolesAsync();
}

public class HeroService : IHeroService
{
    private readonly HttpClient _httpClient;

    public HeroService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<HeroSummaryDto>> GetHeroesAsync(string? role = null, string? type = null, string? search = null)
    {
        var queryParams = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(role))
            queryParams.Add($"role={Uri.EscapeDataString(role)}");
        if (!string.IsNullOrWhiteSpace(type))
            queryParams.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        var url = "api/heroes";
        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<List<HeroSummaryDto>>(url) ?? new List<HeroSummaryDto>();
    }

    public async Task<HeroDetailDto?> GetHeroAsync(string shortName)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HeroDetailDto>($"api/heroes/{shortName}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<string>> GetRolesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<string>>("api/heroes/roles") ?? new List<string>();
    }
}
