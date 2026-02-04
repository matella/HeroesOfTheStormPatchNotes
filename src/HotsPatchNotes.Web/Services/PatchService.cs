using System.Net.Http.Json;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IPatchService
{
    Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(string? patchType = null, int page = 1, int pageSize = 20);
    Task<PatchDetailDto?> GetPatchAsync(string internalId);
    Task<List<string>> GetPatchTypesAsync();
}

public class PatchService : IPatchService
{
    private readonly HttpClient _httpClient;

    public PatchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(string? patchType = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        
        if (!string.IsNullOrWhiteSpace(patchType))
            queryParams.Add($"patchType={Uri.EscapeDataString(patchType)}");

        var url = "api/patches?" + string.Join("&", queryParams);

        return await _httpClient.GetFromJsonAsync<PagedResultDto<PatchSummaryDto>>(url) 
            ?? new PagedResultDto<PatchSummaryDto>();
    }

    public async Task<PatchDetailDto?> GetPatchAsync(string internalId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PatchDetailDto>($"api/patches/{internalId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<string>> GetPatchTypesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<string>>("api/patches/types") ?? new List<string>();
    }
}
