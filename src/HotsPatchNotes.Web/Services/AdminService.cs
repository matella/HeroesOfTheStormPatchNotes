using System.Net.Http.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IAdminService
{
    Task<PagedResult<AdminHeroDto>?> GetHeroesAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<bool> UpdateHeroAsync(AdminHeroDto hero);
    Task<bool> DeleteHeroAsync(int id);
    Task<List<AdminAbilityDto>> GetAbilitiesAsync(int heroId);
    Task<bool> UpdateAbilityAsync(AdminAbilityDto ability);
    Task<List<AdminTalentDto>> GetTalentsAsync(int heroId);
    Task<bool> UpdateTalentAsync(AdminTalentDto talent);
    Task<PagedResult<AdminPatchDto>?> GetPatchesAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<bool> UpdatePatchAsync(AdminPatchDto patch);
    Task<bool> DeletePatchAsync(int id);
    Task<List<AdminBattlegroundDto>> GetBattlegroundsAsync();
    Task<bool> UpdateBattlegroundAsync(AdminBattlegroundDto battleground);
    Task<bool> DeleteBattlegroundAsync(int id);
}

public sealed class AdminService(HttpClient httpClient) : IAdminService
{
    private const string BaseRoute = Constants.ApiRoutes.Admin;

    public async Task<PagedResult<AdminHeroDto>?> GetHeroesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        try
        {
            var url = $"{BaseRoute}/heroes?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";
            return await httpClient.GetFromJsonAsync<PagedResult<AdminHeroDto>>(url);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> UpdateHeroAsync(AdminHeroDto hero)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/heroes/{hero.Id}", hero);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteHeroAsync(int id)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{BaseRoute}/heroes/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<AdminAbilityDto>> GetAbilitiesAsync(int heroId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<AdminAbilityDto>>($"{BaseRoute}/heroes/{heroId}/abilities") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<bool> UpdateAbilityAsync(AdminAbilityDto ability)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/abilities/{ability.Id}", ability);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<AdminTalentDto>> GetTalentsAsync(int heroId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<AdminTalentDto>>($"{BaseRoute}/heroes/{heroId}/talents") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<bool> UpdateTalentAsync(AdminTalentDto talent)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/talents/{talent.Id}", talent);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<PagedResult<AdminPatchDto>?> GetPatchesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        try
        {
            var url = $"{BaseRoute}/patches?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
                url += $"&search={Uri.EscapeDataString(search)}";
            return await httpClient.GetFromJsonAsync<PagedResult<AdminPatchDto>>(url);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<bool> UpdatePatchAsync(AdminPatchDto patch)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/patches/{patch.Id}", patch);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<bool> DeletePatchAsync(int id)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{BaseRoute}/patches/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<AdminBattlegroundDto>> GetBattlegroundsAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<AdminBattlegroundDto>>($"{BaseRoute}/battlegrounds") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<bool> UpdateBattlegroundAsync(AdminBattlegroundDto battleground)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{BaseRoute}/battlegrounds/{battleground.Id}", battleground);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteBattlegroundAsync(int id)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{BaseRoute}/battlegrounds/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
