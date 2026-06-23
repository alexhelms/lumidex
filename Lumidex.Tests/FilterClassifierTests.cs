using Lumidex.Core.Targets;

namespace Lumidex.Tests;

public class FilterClassifierTests
{
    [Theory]
    [InlineData("L", FilterBand.Broadband)]
    [InlineData("Red", FilterBand.Broadband)]
    [InlineData("Ha", FilterBand.Narrowband)]
    [InlineData("OIII", FilterBand.Narrowband)]
    [InlineData("V", FilterBand.Photometric)]
    [InlineData("B", FilterBand.Photometric)]
    [InlineData("Color", FilterBand.OscMultiband)]
    [InlineData("L-eNhance", FilterBand.OscMultiband)]
    [InlineData("ha", FilterBand.Narrowband)]           // case-insensitive known label
    [InlineData("color", FilterBand.OscMultiband)]      // case-insensitive known label
    public void BandOf_KnownCanonicalLabels(string filter, FilterBand expected)
        => FilterClassifier.BandOf(filter).Should().Be(expected);

    [Theory]
    [InlineData("Halpha 3nm", FilterBand.Narrowband)]   // emission token in an unknown name
    [InlineData("OIII 6.5nm", FilterBand.Narrowband)]   // token + bandwidth
    [InlineData("5nm", FilterBand.Narrowband)]          // narrow bandwidth alone
    [InlineData("12nm", FilterBand.Narrowband)]         // bandwidth boundary (<= 12)
    [InlineData("13nm", FilterBand.Unknown)]            // just over the narrowband boundary
    [InlineData("50nm", FilterBand.Unknown)]            // broadband bandwidth, not narrowband
    [InlineData("g'", FilterBand.Photometric)]          // Sloan
    [InlineData("i'", FilterBand.Photometric)]          // Sloan
    [InlineData("L-Quad", FilterBand.OscMultiband)]     // OSC keyword (Quad)
    [InlineData("Duo Ha", FilterBand.OscMultiband)]     // OSC keyword wins over the NB token (check order)
    [InlineData("Sheet", FilterBand.Unknown)]           // "he" is mid-word → NOT narrowband (word-boundary guard)
    [InlineData("Clear", FilterBand.Unknown)]           // genuinely unrecognised
    public void BandOf_UnknownNames_ByPattern(string raw, FilterBand expected)
        => FilterClassifier.BandOf(raw).Should().Be(expected);

    // The full static order: bands in fixed order, then within-band — broadband L-first then
    // RGB desc; narrowband with the SHO trio pinned first then the rest by wavelength desc;
    // photometric asc (UBVRI); OSC curated.
    [Fact]
    public void SortKey_OrdersTheKnownFilters()
    {
        var shuffled = new[]
        {
            "I", "Ha", "Blue", "Color", "L", "OIII", "U", "Green", "LPro", "SII",
            "V", "Red", "He", "B", "NII", "L-Ultimate", "Hb", "R", "L-eNhance",
        };

        var ordered = shuffled.OrderBy(FilterClassifier.SortKey).ToArray();

        ordered.Should().Equal(
            "L", "Red", "Green", "Blue",             // broadband
            "SII", "Ha", "OIII", "NII", "Hb", "He",  // narrowband: SHO pinned, then NII/Hb/He by wavelength
            "U", "B", "V", "R", "I",                 // photometric, wavelength asc (UBVRI)
            "Color", "L-eNhance", "L-Ultimate", "LPro"); // OSC curated
    }

    // An unknown narrowband filter classifies into the NB band and sorts after the known NB
    // ones (unknown wavelength → band tail) — never leaking into another band.
    [Fact]
    public void SortKey_UnknownNarrowband_SlotsAtNarrowbandTail()
    {
        var items = new[] { "Color", "Ha", "L", "Ha-band", "U" };  // Ha-band → NB by the word-boundary "Ha" token

        var ordered = items.OrderBy(FilterClassifier.SortKey).ToArray();

        ordered.Should().Equal("L", "Ha", "Ha-band", "U", "Color");
    }

    // The asc-band twin of the test above: an unknown PHOTOMETRIC filter (Sloan z', absent
    // from the wavelength table) sorts to the photometric tail, after I — not to the head.
    [Fact]
    public void SortKey_UnknownPhotometric_SlotsAtPhotometricTail()
    {
        var items = new[] { "Ha", "V", "z'", "I", "Color" };

        var ordered = items.OrderBy(FilterClassifier.SortKey).ToArray();

        ordered.Should().Equal("Ha", "V", "I", "z'", "Color");
    }
}
