namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for downloading and caching images locally.
/// </summary>
public interface IImageDownloadService
{
    /// <summary>
    /// Downloads an image from a URL and saves it locally.
    /// </summary>
    /// <param name="imageUrl">The URL of the image to download.</param>
    /// <param name="category">Category folder (e.g., "heroes", "battlegrounds").</param>
    /// <param name="fileName">The filename to save as (without extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The relative path to the saved image, or null if download failed.</returns>
    Task<string?> DownloadImageAsync(string imageUrl, string category, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an image already exists locally.
    /// </summary>
    /// <param name="category">Category folder (e.g., "heroes", "battlegrounds").</param>
    /// <param name="fileName">The filename to check (without extension).</param>
    /// <returns>The relative path if the image exists, otherwise null.</returns>
    string? GetLocalImagePath(string category, string fileName);
}
