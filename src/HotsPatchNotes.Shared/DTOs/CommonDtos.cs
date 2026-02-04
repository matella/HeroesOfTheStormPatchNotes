namespace HotsPatchNotes.Shared.DTOs;

/// <summary>
/// Response for data sync operations.
/// </summary>
public class SyncResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int HeroesUpdated { get; set; }
    public int PatchesUpdated { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Generic paginated response.
/// </summary>
public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// API error response.
/// </summary>
public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public int StatusCode { get; set; }
}
