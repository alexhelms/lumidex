namespace Lumidex.Core.Targets;

// Folds the many ways a filter gets named into one canonical label, so the integration bars merge
// true synonyms (e.g. "H" and "Ha", or "Luminance" and "L") into a single segment and color them
// consistently — while keeping genuinely different filters apart.
//
// Two filter naming systems coexist in real libraries, and Lumidex serves both:
//   - Imaging (astrophotography): Luminance, Red/Green/Blue (or L/R/G/B), narrowband
//     Ha/OIII/SII/Hb/He/NII, one-shot-color "Color", and multiband LPro / L-eNhance / LUltimate.
//   - Photometric (science): Johnson-Cousins U B V R I.
//
// The ONLY names that overlap between the two systems are the bare letters "B" and "R". Everything
// else is unambiguous: "U", "V", "I" only exist in the photometric system; "Luminance", "Green", the
// narrowband and multiband names, and the colour words only exist in imaging. So the rule needs to
// resolve just B and R, and it does so from context rather than special-casing any telescope:
//
//   A bare B/R is PHOTOMETRIC when the surrounding filter set shows photometric evidence — it
//   contains U, V, or I (bands with no imaging meaning), OR it also carries the imaging WORD form
//   (Blue/Red) of that colour, which means the bare letter must be the other system. Otherwise the
//   letter is the imaging band.
//
// That one rule covers pure-imaging datasets (letters fold to LRGB), pure photometry (UBVRI stays
// photometric), and mixed rigs that shoot both — with no per-telescope exceptions. The
// disambiguation context is the telescope's whole filter set (passed in by the caller), so a scope's
// B is classified the same way for every target on it.
public static class FilterCanonicalizer
{
    // Unambiguous spelling variants -> canonical label (case-insensitive, so only distinct spellings
    // are listed, not casings). The bare letters B and R are deliberately absent — they're resolved
    // in Canonicalize by context.
    private static readonly Dictionary<string, string> Direct = new(StringComparer.OrdinalIgnoreCase)
    {
        ["L"] = "L", ["Lum"] = "L", ["Luminance"] = "L",
        ["Green"] = "Green", ["G"] = "Green",
        ["Red"] = "Red",
        ["Blue"] = "Blue",
        ["Ha"] = "Ha", ["H"] = "Ha", ["Halpha"] = "Ha", ["H-alpha"] = "Ha", ["Hα"] = "Ha",
        ["OIII"] = "OIII", ["O"] = "OIII", ["O3"] = "OIII",
        ["SII"] = "SII", ["S"] = "SII", ["S2"] = "SII",
        ["Hb"] = "Hb", ["Hbeta"] = "Hb", ["H-beta"] = "Hb", ["Hβ"] = "Hb",
        ["He"] = "He", ["HeII"] = "He", ["HeI"] = "He",
        ["NII"] = "NII", ["N"] = "NII", ["N2"] = "NII",
        ["Color"] = "Color", ["Colour"] = "Color", ["OSC"] = "Color",
        ["LPro"] = "LPro", ["L-Pro"] = "LPro",
        ["LUltimate"] = "LUltimate", ["L-Ultimate"] = "LUltimate",
        ["L-eNhance"] = "L-eNhance", ["LeNhance"] = "L-eNhance", ["L-Enhance"] = "L-eNhance", ["LEnhance"] = "L-eNhance",
        ["U"] = "U", ["V"] = "V", ["I"] = "I", // photometric, unambiguous
    };

    // Maps one raw filter name to its canonical label. `datasetFilters` is the set of raw filter
    // names that share this filter's disambiguation context (the telescope's whole filter set) —
    // used only to resolve bare B/R. An empty or unrecognized name is returned unchanged (so
    // "(No filter)" and any filter we don't know about pass through).
    public static string Canonicalize(string? raw, IReadOnlyCollection<string> datasetFilters)
    {
        var name = (raw ?? string.Empty).Trim();
        if (name.Length == 0)
            return raw ?? string.Empty;

        // Resolve the two ambiguous letters first, before the direct table.
        if (name.Equals("B", StringComparison.OrdinalIgnoreCase))
            return IsPhotometric("Blue", datasetFilters) ? "B" : "Blue";
        if (name.Equals("R", StringComparison.OrdinalIgnoreCase))
            return IsPhotometric("Red", datasetFilters) ? "R" : "Red";

        return Direct.TryGetValue(name, out var canonical) ? canonical : name;
    }

    // Photometric evidence for a bare letter: the dataset carries a Johnson-Cousins-only band
    // (U/V/I), or it carries the imaging word-form of the same colour alongside the letter (so the
    // letter must be the other system).
    private static bool IsPhotometric(string imagingWord, IReadOnlyCollection<string> datasetFilters)
    {
        foreach (var f in datasetFilters)
        {
            var t = f.Trim();
            if (t.Equals("U", StringComparison.OrdinalIgnoreCase)
                || t.Equals("V", StringComparison.OrdinalIgnoreCase)
                || t.Equals("I", StringComparison.OrdinalIgnoreCase)
                || t.Equals(imagingWord, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
