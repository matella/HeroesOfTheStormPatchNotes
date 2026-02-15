using System.Text.RegularExpressions;
using Ganss.Xss;

namespace HotsPatchNotes.Api.Services;

public interface IHtmlContentService
{
    string SanitizeHtml(string html);
    string SanitizeGamestring(string gamestring);
}

public sealed partial class HtmlContentService(HtmlSanitizer sanitizer) : IHtmlContentService
{
    public string SanitizeHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        return sanitizer.Sanitize(html);
    }

    /// <summary>
    /// Sanitizes gamestring-specific HTML markup from Heroes of the Storm game files.
    /// Handles special tags like &lt;c val="..."&gt;, &lt;n/&gt;, &lt;s&gt;, and &lt;img&gt; that appear in gamestrings.
    /// </summary>
    public string SanitizeGamestring(string gamestring)
    {
        if (string.IsNullOrWhiteSpace(gamestring))
            return string.Empty;

        var result = gamestring;

        try
        {
            // 1. Convert <n/> newlines to <br/> tags
            result = GamestringNewlineRegex().Replace(result, "<br/>");

            // 2. Strip color tags: <c val="bfd4fd">text</c> → text
            result = GamestringColorTagRegex().Replace(result, "$1");

            // 3. Strip styled spans: <s val="..." name="...">text</s> → text
            result = GamestringStyledSpanRegex().Replace(result, "$1");

            // 4. Remove quest icon images: <img path="..." />
            result = GamestringImageTagRegex().Replace(result, string.Empty);

            // 5. Apply standard HTML sanitization for final cleanup
            result = sanitizer.Sanitize(result);

            return result.Trim();
        }
        catch (RegexMatchTimeoutException ex)
        {
            // Log warning but return partially processed text
            System.Diagnostics.Debug.WriteLine($"Regex timeout while sanitizing gamestring: {ex.Message}");
            return sanitizer.Sanitize(gamestring);
        }
    }

    /// <summary>
    /// Matches gamestring newline tags: &lt;n/&gt; or &lt;n /&gt;
    /// </summary>
    [GeneratedRegex(@"<n\s*/?>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GamestringNewlineRegex();

    /// <summary>
    /// Matches gamestring color tags: &lt;c val="..."&gt;content&lt;/c&gt;
    /// Captures the content in group 1.
    /// </summary>
    [GeneratedRegex(@"<c\s+val=""[^""]*"">(.*?)</c>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GamestringColorTagRegex();

    /// <summary>
    /// Matches gamestring styled span tags: &lt;s val="..." name="..."&gt;content&lt;/s&gt;
    /// Captures the content in group 1.
    /// </summary>
    [GeneratedRegex(@"<s\s+(?:val=""[^""]*""\s*)?(?:name=""[^""]*""\s*)?>(.*?)</s>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GamestringStyledSpanRegex();

    /// <summary>
    /// Matches gamestring image tags: &lt;img path="..." /&gt;
    /// </summary>
    [GeneratedRegex(@"<img\s+[^>]*/>", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GamestringImageTagRegex();

    public static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // Allow safe formatting tags
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.Add("p");
        sanitizer.AllowedTags.Add("br");
        sanitizer.AllowedTags.Add("ul");
        sanitizer.AllowedTags.Add("ol");
        sanitizer.AllowedTags.Add("li");
        sanitizer.AllowedTags.Add("strong");
        sanitizer.AllowedTags.Add("b");
        sanitizer.AllowedTags.Add("em");
        sanitizer.AllowedTags.Add("i");
        sanitizer.AllowedTags.Add("span");
        sanitizer.AllowedTags.Add("a");
        sanitizer.AllowedTags.Add("h1");
        sanitizer.AllowedTags.Add("h2");
        sanitizer.AllowedTags.Add("h3");
        sanitizer.AllowedTags.Add("h4");
        sanitizer.AllowedTags.Add("h5");
        sanitizer.AllowedTags.Add("h6");
        sanitizer.AllowedTags.Add("img");
        sanitizer.AllowedTags.Add("div");

        // Allow safe attributes
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("style");
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");
        sanitizer.AllowedAttributes.Add("src");
        sanitizer.AllowedAttributes.Add("alt");
        sanitizer.AllowedAttributes.Add("title");
        sanitizer.AllowedAttributes.Add("id");

        // Allow safe CSS properties (for color, etc.)
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("color");
        sanitizer.AllowedCssProperties.Add("background-color");
        sanitizer.AllowedCssProperties.Add("font-weight");
        sanitizer.AllowedCssProperties.Add("font-style");
        sanitizer.AllowedCssProperties.Add("text-decoration");

        // Allow safe schemes
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        return sanitizer;
    }
}
