using System.Globalization;
using System.Text.RegularExpressions;

namespace HotsPatchNotes.Api.Services;

/// <summary>
/// Pure classification of a patch section's content: BUFF / NERF / MIXED / REWORK / BUGFIX, plus a
/// short human summary of the largest change ("Q damage 96 → 110 (+15%)"). Heuristic by design —
/// numeric pairs ("from X to Y", "X → Y") drive the verdict, with direction INVERTED for
/// cost/cooldown-like values (an increased cooldown is a nerf). Unit-tested; no I/O.
/// </summary>
public static partial class PatchClassifier
{
    [GeneratedRegex(@"(?<ctx>[^.;\n]{0,70}?)(?:from\s+)?(?<a>\d+(?:\.\d+)?)\s*(?:→|to)\s*(?<b>\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PairRegex();

    [GeneratedRegex(@"\b(rework|redesign|overhaul)", RegexOptions.IgnoreCase)]
    private static partial Regex ReworkRegex();

    [GeneratedRegex(@"\b(cooldown|cost|mana|recharge|cast time|delay)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InvertedMetricRegex();

    [GeneratedRegex(@"\b(fixed|no longer|corrected)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BugfixRegex();

    public sealed record Result(string Classification, string ShortSummary);

    public static Result Classify(string? contentText)
    {
        var text = contentText ?? string.Empty;
        if (ReworkRegex().IsMatch(text))
            return new Result("REWORK", "rework");

        var buffs = 0;
        var nerfs = 0;
        var best = (Pct: 0.0, Summary: string.Empty, IsBuff: false);

        foreach (Match m in PairRegex().Matches(text))
        {
            if (!double.TryParse(m.Groups["a"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ||
                !double.TryParse(m.Groups["b"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ||
                a == 0 || a == b)
                continue;

            var up = b > a;
            var inverted = InvertedMetricRegex().IsMatch(m.Groups["ctx"].Value);
            var isBuff = inverted ? !up : up;
            if (isBuff) buffs++; else nerfs++;

            var pct = Math.Abs((b - a) / a) * 100.0;
            if (pct > best.Pct)
            {
                var label = CleanLabel(m.Groups["ctx"].Value);
                var sign = (b > a) ? "+" : "−";
                var summary = $"{label}{m.Groups["a"].Value} → {m.Groups["b"].Value} ({sign}{Math.Round(pct, pct < 10 ? 1 : 0).ToString(CultureInfo.InvariantCulture)}%)";
                best = (pct, summary, isBuff);
            }
        }

        var classification = (buffs, nerfs) switch
        {
            (> 0, 0) => "BUFF",
            (0, > 0) => "NERF",
            (> 0, > 0) => "MIXED",
            _ => BugfixRegex().IsMatch(text) ? "BUGFIX" : string.Empty,
        };
        return new Result(classification, best.Summary);
    }

    private static string CleanLabel(string ctx)
    {
        // Keep the tail of the context (the words right before the numbers), tidy it up.
        var label = Regex.Replace(ctx, @"\s+", " ").Trim();
        label = Regex.Replace(label, @"^(and|the|now|increased|reduced|lowered|raised)\s+", "",
            RegexOptions.IgnoreCase);
        if (label.Length > 34)
            label = "…" + label[^33..];
        return label.Length > 0 ? label.TrimEnd(':', ' ') + " " : string.Empty;
    }
}
