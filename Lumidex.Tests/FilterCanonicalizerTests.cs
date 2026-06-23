using Lumidex.Core.Targets;

namespace Lumidex.Tests;

// Unit tests for the two-system filter canonicalizer: synonym folding, the bare
// B/R imaging-vs-photometric disambiguation, and pass-through of unknowns. No DB
// needed — the rule is pure given a filter name and its dataset context.
public class FilterCanonicalizerTests
{
    // A conventional imaging rig (letters, no photometric markers): the
    // disambiguation context for the folding cases below.
    private static readonly string[] Imaging = { "L", "R", "G", "B", "H", "O", "S" };

    [Theory]
    [InlineData("H", "Ha")]
    [InlineData("Ha", "Ha")]
    [InlineData("ha", "Ha")]            // case-insensitive
    [InlineData("Halpha", "Ha")]
    [InlineData("Luminance", "L")]
    [InlineData("Lum", "L")]
    [InlineData("L", "L")]
    [InlineData("O", "OIII")]
    [InlineData("OIII", "OIII")]
    [InlineData("S", "SII")]
    [InlineData("G", "Green")]
    [InlineData("Green", "Green")]
    public void FoldsUnambiguousSynonyms(string raw, string expected)
        => FilterCanonicalizer.Canonicalize(raw, Imaging).Should().Be(expected);

    [Theory]
    [InlineData("Hb", "Hb")]
    [InlineData("He", "He")]
    [InlineData("N", "NII")]
    [InlineData("NII", "NII")]
    [InlineData("Color", "Color")]
    [InlineData("LPro", "LPro")]
    [InlineData("LUltimate", "LUltimate")]
    [InlineData("L-eNhance", "L-eNhance")]
    public void CoversLessCommonAndMultibandFilters(string raw, string expected)
        => FilterCanonicalizer.Canonicalize(raw, Imaging).Should().Be(expected);

    // Pure imaging rig (letters only): bare B/R are LRGB bands.
    [Fact]
    public void BareLetters_InImagingSet_AreImagingBands()
    {
        FilterCanonicalizer.Canonicalize("B", Imaging).Should().Be("Blue");
        FilterCanonicalizer.Canonicalize("R", Imaging).Should().Be("Red");
    }

    // Pure photometry rig (UBVRI): the U/V/I markers make B and R photometric.
    [Fact]
    public void BareLetters_WithPhotometricMarkers_ArePhotometric()
    {
        var phot = new[] { "U", "B", "V", "R", "I" };
        FilterCanonicalizer.Canonicalize("B", phot).Should().Be("B");
        FilterCanonicalizer.Canonicalize("R", phot).Should().Be("R");
        FilterCanonicalizer.Canonicalize("V", phot).Should().Be("V");
    }

    // Mixed rig (LRGB words AND photometric letters): the word form alongside the
    // letter forces the letter to the photometric system, so "Blue"/"B" and
    // "Red"/"R" stay DISTINCT.
    [Fact]
    public void BareLetters_AlongsideImagingWord_StayDistinctFromIt()
    {
        var mixed = new[] { "Luminance", "Red", "Green", "Blue", "Ha", "OIII", "SII", "B", "R", "U", "V", "I" };
        FilterCanonicalizer.Canonicalize("B", mixed).Should().Be("B");
        FilterCanonicalizer.Canonicalize("Blue", mixed).Should().Be("Blue");
        FilterCanonicalizer.Canonicalize("R", mixed).Should().Be("R");
        FilterCanonicalizer.Canonicalize("Red", mixed).Should().Be("Red");
    }

    // The word-form rule fires even without U/V/I: a set carrying both "Red" and "R"
    // means the bare letter is the non-imaging system.
    [Fact]
    public void BareLetter_WithWordFormButNoUVI_IsPhotometric()
    {
        var set = new[] { "Red", "R", "Blue", "B" };
        FilterCanonicalizer.Canonicalize("R", set).Should().Be("R");
        FilterCanonicalizer.Canonicalize("B", set).Should().Be("B");
    }

    [Theory]
    [InlineData("(No filter)", "(No filter)")]
    [InlineData("", "")]
    [InlineData("Tricolor", "Tricolor")]    // genuinely unknown — passes through, colors gray
    public void PassesThroughEmptyAndUnknown(string raw, string expected)
        => FilterCanonicalizer.Canonicalize(raw, Imaging).Should().Be(expected);

    // The flip map covers exactly the four context-dependent labels (case-insensitively) and
    // nothing else — goal persistence relies on every other label resolving to null here.
    [Theory]
    [InlineData("B", "Blue")]
    [InlineData("blue", "B")]
    [InlineData("r", "Red")]
    [InlineData("RED", "R")]
    [InlineData("L", null)]
    [InlineData("Ha", null)]
    [InlineData("V", null)]
    public void FlipPartnerOf_MapsOnlyTheFourFlippableLabels(string label, string? expected)
        => FilterCanonicalizer.FlipPartnerOf(label).Should().Be(expected);
}
