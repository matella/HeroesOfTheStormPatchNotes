using System.Net.Http.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IHeroService
{
    Task<List<HeroSummaryDto>> GetHeroesAsync(string? role = null, string? type = null, string? search = null);
    Task<HeroDetailDto?> GetHeroAsync(string shortName);
    Task<List<string>> GetRolesAsync();
    Task<List<HeroBuildDto>> GetHeroBuildsAsync(string shortName);
    Task<HeroBuildDto?> CreateBuildAsync(string shortName, CreateBuildDto build);
    Task<List<HeroPatchDto>> GetHeroPatchesAsync(string shortName);
}

public class HeroService(HttpClient httpClient) : IHeroService
{
    private const string BaseRoute = Constants.ApiRoutes.Heroes;

    public async Task<List<HeroSummaryDto>> GetHeroesAsync(string? role = null, string? type = null, string? search = null)
    {
        try
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(role))
                queryParams.Add($"role={Uri.EscapeDataString(role)}");
            if (!string.IsNullOrWhiteSpace(type))
                queryParams.Add($"type={Uri.EscapeDataString(type)}");
            if (!string.IsNullOrWhiteSpace(search))
                queryParams.Add($"search={Uri.EscapeDataString(search)}");

            var url = BaseRoute;
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);

            return await httpClient.GetFromJsonAsync<List<HeroSummaryDto>>(url) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<HeroDetailDto?> GetHeroAsync(string shortName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<HeroDetailDto>($"{BaseRoute}/{shortName}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<string>> GetRolesAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<string>>($"{BaseRoute}/roles") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<List<HeroBuildDto>> GetHeroBuildsAsync(string shortName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<HeroBuildDto>>($"{BaseRoute}/{shortName}/builds") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<HeroBuildDto?> CreateBuildAsync(string shortName, CreateBuildDto build)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{BaseRoute}/{shortName}/builds", build);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<HeroBuildDto>();
            }
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<HeroPatchDto>> GetHeroPatchesAsync(string shortName)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<HeroPatchDto>>($"{BaseRoute}/{shortName}/patches") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
