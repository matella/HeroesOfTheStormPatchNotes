using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HotsPatchNotes.Web.Services;

/// <summary>
/// Turns numeric change pairs ("8 → 7", "increased from 96 to 110") inside patch-note HTML into
/// colored monospace diffs with a computed delta — the "patch notes are diffs" pillar. Operates on
/// text nodes only so markup and attributes are never rewritten.
/// </summary>
public static partial class DiffColorizer
{
    [GeneratedRegex("(<[^>]*>)")]
    private static partial Regex TagSplitRegex();

    [GeneratedRegex(@"(?<a>\d+(?:[.,]\d+)?)(?<ua>\s?%|s\b)?(?:\s*(?:→|&#8594;|&rarr;)\s*|\s+to\s+)(?<b>\d+(?:[.,]\d+)?)(?<ub>\s?%|s\b)?")]
    private static partial Regex PairRegex();

    public static string Colorize(string html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        var parts = TagSplitRegex().Split(html);
        var sb = new StringBuilder(html.Length + 256);
        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;
            sb.Append(part.StartsWith('<') ? part : PairRegex().Replace(part, ReplacePair));
        }
        return sb.ToString();
    }

    private static string ReplacePair(Match m)
    {
        if (!TryParse(m.Groups["a"].Value, out var a) || !TryParse(m.Groups["b"].Value, out var b)
            || a == b)
            return m.Value;

        var up = b > a;
        var valCls = up ? "diff-up" : "diff-down";
        var pctCls = up ? "diff-up-pct" : "diff-down-pct";
        var pct = "";
        if (a > 0)
        {
            var delta = (b - a) / a * 100;
            var txt = Math.Abs(delta).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');
            pct = $" <span class=\"{pctCls}\">({(up ? "+" : "−")}{txt}%)</span>";
        }

        var ua = m.Groups["ua"].Value;
        var ub = m.Groups["ub"].Value;
        return $"<span class=\"diff mono\"><span class=\"diff-old\">{m.Groups["a"].Value}{ua}</span>" +
               $" → <span class=\"{valCls}\">{m.Groups["b"].Value}{ub}</span>{pct}</span>";
    }

    private static bool TryParse(string s, out double value) =>
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
