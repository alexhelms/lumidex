using System.Text.RegularExpressions;

namespace Lumidex.Core.Targets;

// Which naming system a filter belongs to. The Target Summary renders filter segments in
// this band order (Broadband first), then by wavelength within a band — so a filter Lumidex
// has never seen still lands in the right section automatically, with no per-filter entry.
public enum FilterBand
{
    Broadband = 0,    // L, Red, Green, Blue
    Narrowband = 1,   // Ha, OIII, SII, NII, Hb, He, ... (emission lines)
    Photometric = 2,  // Johnson-Cousins U B V R I, Sloan u' g' r' i' z'
    OscMultiband = 3, // one-shot colour + dual/tri-band (L-eNhance, L-Ultimate, ...)
    Unknown = 4,
}

// Classifies a filter and produces a stable sort key, by RULE (naming conventions +
// physics), no hard-coded master list and no ML. Dustin's requirement: determine a filter's
// type and slot it in automatically rather than enumerate every filter imaginable.
//
// The input is the FilterCanonicalizer label. For filters the canonicalizer knows, that's a
// clean label ("Ha", "V", ...); for ones it doesn't, the canonicalizer returns the raw FITS
// name unchanged, so the same string still carries the tokens/bandwidth we pattern-match on.
public static partial class FilterClassifier
{
    // Approx central wavelength (nm) per known label — used only to ORDER within a band, not
    // to identify filters. Physics, deliberately short: an unknown filter takes a wavelength
    // from an embedded "NNNnm" if present, else sorts to its band's tail.
    private static readonly Dictionary<string, double> Wavelength = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Red"] = 610, ["Green"] = 530, ["Blue"] = 470,                  // broadband colour
        ["SII"] = 672, ["NII"] = 658, ["Ha"] = 656, ["OIII"] = 500,       // narrowband ...
        ["Hb"] = 486, ["He"] = 468, ["OII"] = 372,                       // ... emission lines
        ["U"] = 365, ["B"] = 445, ["V"] = 551, ["R"] = 658, ["I"] = 806,  // Johnson-Cousins
    };

    // Canonical labels grouped by band (the set FilterCanonicalizer can produce).
    private static readonly HashSet<string> BroadbandLabels = new(StringComparer.OrdinalIgnoreCase) { "L", "Red", "Green", "Blue" };
    private static readonly HashSet<string> PhotometricLabels = new(StringComparer.OrdinalIgnoreCase) { "U", "B", "V", "R", "I" };
    private static readonly HashSet<string> NarrowbandLabels = new(StringComparer.OrdinalIgnoreCase) { "SII", "Ha", "OIII", "NII", "Hb", "He", "OII" };
    // OSC/multiband, in display order; the index is the within-band rank.
    private static readonly string[] OscOrder = { "Color", "L-eNhance", "L-Ultimate", "LPro" };
    // Hubble-palette trio, pinned ahead of the rest of the narrowband band: keep SHO visually
    // grouped rather than let NII's 658 nm split it under pure wavelength-descending order.
    private static readonly string[] ShoLead = { "SII", "Ha", "OIII" };

    private const double Tail = 1e6;   // parks an unknown-wavelength filter at its band's tail

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*nm", RegexOptions.IgnoreCase)] private static partial Regex NmRegex();
    [GeneratedRegex(@"^[ugriz]'?$", RegexOptions.IgnoreCase)] private static partial Regex SloanRegex();
    // Emission-line tokens marking an unknown name narrowband. Anchored with \b (word START)
    // so a token matches at a word boundary — "HeII"/"Halpha" still match, but a token that is
    // only a mid-word substring (the "he" in "Sheet") does not, avoiding false narrowband hits.
    [GeneratedRegex(@"\b(Halpha|OIII|SII|NII|Hbeta|Ha|Hb|OII|He|NV|CIV)", RegexOptions.IgnoreCase)] private static partial Regex NbTokenRegex();
    // Dual/tri-band & OSC keywords, same word-boundary guard.
    [GeneratedRegex(@"\b(OSC|Colou?r|Duo|Tri|Quad|UHC|CLS|eNhance|Ultimate|L-?Pro)", RegexOptions.IgnoreCase)] private static partial Regex OscTokenRegex();

    // The band a filter belongs to. `filter` is the FilterCanonicalizer label (= the raw FITS
    // name for filters the canonicalizer didn't fold, which is what the pattern fallbacks read).
    public static FilterBand BandOf(string filter)
    {
        var name = (filter ?? string.Empty).Trim();
        if (BroadbandLabels.Contains(name)) return FilterBand.Broadband;
        if (PhotometricLabels.Contains(name)) return FilterBand.Photometric;
        if (NarrowbandLabels.Contains(name)) return FilterBand.Narrowband;
        if (OscOrder.Contains(name, StringComparer.OrdinalIgnoreCase)) return FilterBand.OscMultiband;

        // Unrecognised label → pattern recognition on the (raw) name. Order matters: Sloan and
        // OSC keywords are checked before the broader narrowband token scan.
        if (SloanRegex().IsMatch(name)) return FilterBand.Photometric;
        if (OscTokenRegex().IsMatch(name)) return FilterBand.OscMultiband;
        if (NbTokenRegex().IsMatch(name)) return FilterBand.Narrowband;
        // A narrow bandwidth (<= ~12 nm) embedded in the name marks narrowband.
        var m = NmRegex().Match(name);
        if (m.Success && double.TryParse(m.Groups[1].Value, out var nm) && nm <= 12) return FilterBand.Narrowband;

        return FilterBand.Unknown;
    }

    // A comparable sort key. Sorting ASCENDING gives: bands in fixed order, then within a band
    // — broadband L-first then R/G/B by wavelength DESCENDING; narrowband by wavelength
    // DESCENDING (Hubble convention); photometric by wavelength ASCENDING (catalog
    // convention); OSC by its curated order; unknown filters alphabetical and last. The final
    // element breaks ties by label so the order is stable.
    public static (int Band, double Within, string Tie) SortKey(string filter)
    {
        var name = (filter ?? string.Empty).Trim();
        var band = BandOf(name);
        double? nm = WavelengthOf(name);
        double within = band switch
        {
            FilterBand.Broadband => name.Equals("L", StringComparison.OrdinalIgnoreCase)
                ? double.MinValue                       // L (panchromatic) pinned first
                : (nm is double w ? -w : Tail),         // R/G/B descending; unknown last
            FilterBand.Narrowband => ShoIndex(name) is double s ? -1e6 + s   // SHO pinned first (S-H-O)
                : nm is double n ? -n : Tail,                                // then the rest, wavelength desc; unknown last
            FilterBand.Photometric => nm ?? Tail,                  // ascending; unknown last
            FilterBand.OscMultiband => OscIndex(name),
            _ => 0,
        };
        return ((int)band, within, name);
    }

    // Wavelength for ordering: the known table, else an embedded "NNNnm", else null (tail).
    private static double? WavelengthOf(string name)
    {
        if (Wavelength.TryGetValue(name, out var nm)) return nm;
        var m = NmRegex().Match(name);
        return m.Success && double.TryParse(m.Groups[1].Value, out var parsed) ? parsed : null;
    }

    private static double OscIndex(string name)
    {
        var i = Array.FindIndex(OscOrder, o => o.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : OscOrder.Length;   // an unknown OSC keyword sorts after the curated ones
    }

    // Index of a Hubble-palette filter (SII, Ha, OIII) so it pins ahead of the NB tail in
    // S-H-O order; null for any other narrowband filter (which then sorts by wavelength).
    private static double? ShoIndex(string name)
    {
        var i = Array.FindIndex(ShoLead, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 ? i : null;
    }
}
