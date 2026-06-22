using Lumidex.Core.Targets;

namespace Lumidex.Tests;

// Pure (no DB) tests of the filter-name folding and the bare-B/R disambiguation.
public class FilterCanonicalizerTests
{
    private static readonly string[] None = [];

    [Theory]
    [InlineData("H", "Ha")]
    [InlineData("Ha", "Ha")]
    [InlineData("Halpha", "Ha")]
    [InlineData("Lum", "L")]
    [InlineData("Luminance", "L")]
    [InlineData("G", "Green")]
    [InlineData("O", "OIII")]
    [InlineData("S", "SII")]
    [InlineData("OSC", "Color")]
    public void Canonicalize_FoldsUnambiguousSynonyms(string raw, string expected)
        => FilterCanonicalizer.Canonicalize(raw, None).Should().Be(expected);

    [Fact]
    public void Canonicalize_UnknownFilter_PassesThrough()
        => FilterCanonicalizer.Canonicalize("Tri-Band", None).Should().Be("Tri-Band");

    // A bare B/R with no photometric evidence is the imaging band.
    [Fact]
    public void Canonicalize_BareLetters_WithoutPhotometricEvidence_AreImaging()
    {
        FilterCanonicalizer.Canonicalize("B", new[] { "L", "R", "G", "B" }).Should().Be("Blue");
        FilterCanonicalizer.Canonicalize("R", new[] { "L", "R", "G", "B" }).Should().Be("Red");
    }

    // A Johnson-Cousins band (U/V/I) in the set marks bare B/R as photometric.
    [Fact]
    public void Canonicalize_BareLetters_WithUVI_ArePhotometric()
    {
        var set = new[] { "U", "B", "V", "R", "I" };
        FilterCanonicalizer.Canonicalize("B", set).Should().Be("B");
        FilterCanonicalizer.Canonicalize("R", set).Should().Be("R");
    }

    // The imaging WORD form alongside the bare letter marks the letter as the other (photometric)
    // system — a scope shooting both Blue and B keeps them distinct.
    [Fact]
    public void Canonicalize_BareLetter_AlongsideWordForm_IsPhotometric()
    {
        var set = new[] { "Blue", "B", "Red", "R" };
        FilterCanonicalizer.Canonicalize("B", set).Should().Be("B");
        FilterCanonicalizer.Canonicalize("Blue", set).Should().Be("Blue");
    }
}
