using System.Security.Cryptography;
using System.Text;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Service for downloading and caching images locally in wwwroot.
/// </summary>
public sealed class ImageDownloadService(
    HttpClient httpClient,
    IWebHostEnvironment environment,
    ILogger<ImageDownloadService> logger) : IImageDownloadService
{
    private const string ImagesFolder = "images";
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    public async Task<string?> DownloadImageAsync(
        string imageUrl,
        string category,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            logger.LogWarning("Image URL is empty, skipping download");
            return null;
        }

        // Skip data URIs (base64 encoded images)
        if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Skipping data URI image");
            return null;
        }

        try
        {
            // Check if already exists
            var existingPath = GetLocalImagePath(category, fileName);
            if (existingPath != null)
            {
                logger.LogDebug("Image already exists locally: {Path}", existingPath);
                return existingPath;
            }

            // Ensure directory exists
            var categoryPath = Path.Combine(environment.WebRootPath, ImagesFolder, category);
            Directory.CreateDirectory(categoryPath);

            // Download image
            logger.LogInformation("Downloading image from {Url}", imageUrl);
            var imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);

            // Determine file extension from URL or content type
            var extension = GetImageExtension(imageUrl);
            if (extension == null)
            {
                logger.LogWarning("Could not determine image extension for {Url}", imageUrl);
                return null;
            }

            // Save to disk
            var safeFileName = SanitizeFileName(fileName);
            var fullFileName = $"{safeFileName}{extension}";
            var filePath = Path.Combine(categoryPath, fullFileName);

            await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

            // Return relative path for web serving
            var relativePath = $"/{ImagesFolder}/{category}/{fullFileName}";
            logger.LogInformation("Image saved to {Path}", relativePath);

            return relativePath;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download image from {Url}", imageUrl);
            return null;
        }
    }

    public string? GetLocalImagePath(string category, string fileName)
    {
        var safeFileName = SanitizeFileName(fileName);
        var categoryPath = Path.Combine(environment.WebRootPath, ImagesFolder, category);

        // Check for any supported extension
        foreach (var ext in SupportedExtensions)
        {
            var filePath = Path.Combine(categoryPath, $"{safeFileName}{ext}");
            if (File.Exists(filePath))
            {
                return $"/{ImagesFolder}/{category}/{safeFileName}{ext}";
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts file extension from URL or defaults to .jpg.
    /// </summary>
    private static string? GetImageExtension(string url)
    {
        try
        {
            // Parse URL and get the path
            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            // Remove query string if present
            var pathWithoutQuery = path.Split('?')[0];

            // Get extension
            var extension = Path.GetExtension(pathWithoutQuery).ToLowerInvariant();

            // Validate it's a supported image extension
            if (SupportedExtensions.Contains(extension))
            {
                return extension;
            }

            // Default to .jpg if no valid extension found
            return ".jpg";
        }
        catch
        {
            return ".jpg";
        }
    }

    /// <summary>
    /// Sanitizes a filename by removing invalid characters and limiting length.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            // Generate random name if empty
            return GenerateRandomFileName();
        }

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", fileName.Split(invalidChars));

        // Replace spaces and special chars with hyphens
        sanitized = sanitized
            .Replace(" ", "-")
            .Replace("'", "")
            .Replace("\"", "")
            .ToLowerInvariant();

        // Limit length
        if (sanitized.Length > 100)
        {
            sanitized = sanitized.Substring(0, 100);
        }

        // Ensure not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return GenerateRandomFileName();
        }

        return sanitized.Trim('-');
    }

    /// <summary>
    /// Generates a random filename using a hash.
    /// </summary>
    private static string GenerateRandomFileName()
    {
        var bytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        var hash = MD5.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
