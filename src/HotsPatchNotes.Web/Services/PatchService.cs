using System.Net.Http.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IPatchService
{
    Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(string? patchType = null, int page = Constants.Pagination.DefaultPage, int pageSize = Constants.Pagination.DefaultPageSize);
    Task<ReconstructedPatchDto?> GetPatchAsync(string internalId);
    Task<List<string>> GetPatchTypesAsync();
}

public sealed class PatchService(HttpClient httpClient) : IPatchService
{
    private const string BaseRoute = Constants.ApiRoutes.Patches;

    public async Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(string? patchType = null, int page = Constants.Pagination.DefaultPage, int pageSize = Constants.Pagination.DefaultPageSize)
    {
        try
        {
            var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };

            if (!string.IsNullOrWhiteSpace(patchType))
            {
                queryParams.Add($"patchType={Uri.EscapeDataString(patchType)}");
            }

            var url = $"{BaseRoute}?{string.Join("&", queryParams)}";

            return await httpClient.GetFromJsonAsync<PagedResultDto<PatchSummaryDto>>(url)
                ?? new PagedResultDto<PatchSummaryDto>();
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return new PagedResultDto<PatchSummaryDto>();
        }
    }

    public async Task<ReconstructedPatchDto?> GetPatchAsync(string internalId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ReconstructedPatchDto>($"{BaseRoute}/{internalId}");
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<List<string>> GetPatchTypesAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<string>>($"{BaseRoute}/types") ?? [];
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
