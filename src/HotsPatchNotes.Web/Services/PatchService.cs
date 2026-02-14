using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotsPatchNotes.Shared;
using HotsPatchNotes.Shared.DTOs;

namespace HotsPatchNotes.Web.Services;

public interface IPatchService
{
    Task<PagedResultDto<PatchSummaryDto>> GetPatchesAsync(string? patchType = null, int page = Constants.Pagination.DefaultPage, int pageSize = Constants.Pagination.DefaultPageSize);
    Task<ReconstructedPatchDto?> GetPatchAsync(string internalId);
    Task<List<string>> GetPatchTypesAsync();
}

public sealed class PatchService(HttpClient httpClient, IErrorStateService errorState) : IPatchService
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
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is not null && ex.StatusCode != HttpStatusCode.NotFound)
            {
                errorState.SetError(
                    "Unable to load patches",
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable
                        ? "The server is temporarily unavailable. Please try again later."
                        : "Network connection failed. Check your connection.",
                    ErrorSeverity.Error);
            }
            return new PagedResultDto<PatchSummaryDto>();
        }
        catch (JsonException)
        {
            errorState.SetError(
                "Data format error",
                "Received invalid data from server. Please refresh the page.",
                ErrorSeverity.Warning);
            return new PagedResultDto<PatchSummaryDto>();
        }
    }

    public async Task<ReconstructedPatchDto?> GetPatchAsync(string internalId)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ReconstructedPatchDto>($"{BaseRoute}/{internalId}");
        }
        catch (HttpRequestException ex)
        {
            // Don't set error for 404s (expected for non-existent patches)
            if (ex.StatusCode is not null && ex.StatusCode != HttpStatusCode.NotFound)
            {
                errorState.SetError(
                    "Unable to load patch details",
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

    public async Task<List<string>> GetPatchTypesAsync()
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<string>>($"{BaseRoute}/types") ?? [];
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode is not null && ex.StatusCode != HttpStatusCode.NotFound)
            {
                errorState.SetError(
                    "Unable to load patch types",
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
}
