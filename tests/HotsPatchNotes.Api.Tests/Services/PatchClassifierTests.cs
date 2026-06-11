using Xunit;
using HotsPatchNotes.Api.Services;

namespace HotsPatchNotes.Api.Tests.Services;

/// <summary>
/// Pure classification heuristics: buff/nerf direction (inverted for cooldown/cost), rework and
/// bugfix detection, and the short summary of the largest change.
/// </summary>
public sealed class PatchClassifierTests
{
    [Fact]
    public void IncreasedDamage_IsBuff_WithSummary()
    {
        var r = PatchClassifier.Classify("Thunder Clap damage increased from 96 to 110.");
        Assert.Equal("BUFF", r.Classification);
        Assert.Contains("96 → 110", r.ShortSummary);
        Assert.Contains("+15", r.ShortSummary);
    }

    [Fact]
    public void ReducedHealth_IsNerf()
    {
        var r = PatchClassifier.Classify("Base maximum Health reduced from 2890 to 2760.");
        Assert.Equal("NERF", r.Classification);
        Assert.Contains("−4.5%", r.ShortSummary);
    }

    [Fact]
    public void IncreasedCooldown_IsNerf_DirectionInverted()
    {
        var r = PatchClassifier.Classify("Cooldown increased from 8 to 10 seconds.");
        Assert.Equal("NERF", r.Classification);
    }

    [Fact]
    public void ReducedManaCost_IsBuff_DirectionInverted()
    {
        var r = PatchClassifier.Classify("Mana cost reduced from 40 to 35.");
        Assert.Equal("BUFF", r.Classification);
    }

    [Fact]
    public void BuffAndNerf_IsMixed()
    {
        var r = PatchClassifier.Classify(
            "Damage increased from 100 to 120. Health reduced from 2000 to 1800.");
        Assert.Equal("MIXED", r.Classification);
    }

    [Fact]
    public void ReworkKeyword_WinsOverNumbers()
    {
        var r = PatchClassifier.Classify("Talents reworked. Damage increased from 10 to 20.");
        Assert.Equal("REWORK", r.Classification);
    }

    [Fact]
    public void FixedOnly_IsBugfix()
    {
        var r = PatchClassifier.Classify("Fixed an issue where Avatar no longer interrupted channels.");
        Assert.Equal("BUGFIX", r.Classification);
        Assert.Equal(string.Empty, r.ShortSummary);
    }

    [Fact]
    public void NoSignal_IsEmpty()
    {
        var r = PatchClassifier.Classify("Developer comment: we like where this hero is.");
        Assert.Equal(string.Empty, r.Classification);
    }

    [Fact]
    public void ArrowNotation_IsParsed()
    {
        var r = PatchClassifier.Classify("Slow duration 2 → 2.5 seconds.");
        Assert.Equal("BUFF", r.Classification);
        Assert.Contains("2 → 2.5", r.ShortSummary);
    }
}
