using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Globalization;

namespace Lumidex.Converters;

// Maps a filter's CANONICAL label to its bar-segment color. The filter names are
// already folded to canonical labels upstream by
// Lumidex.Core.Targets.FilterCanonicalizer (so "H"/"Ha" arrive here as "Ha", and a
// telescope's photometric "B" arrives distinct from imaging "Blue") — this
// converter only owns the label -> color mapping, not the folding. Any label not
// in the palette (including the "(No filter)" bucket and genuinely unknown
// filters) falls back to a neutral gray so every segment still renders. Returns an
// IBrush so it binds straight to a Rectangle Fill with no second converter.
//
// Colors are grouped by the two filter systems the canonicalizer distinguishes:
//   - Imaging: Luminance, RGB, the SHO + extra narrowbands, OSC "Color", and the
//     multiband filters, each its own color.
//   - Photometric (Johnson-Cousins U B V R I): deliberately shifted shades of the
//     same hue family as their imaging cousins, so a photometric "B"/"R" reads as
//     blue/red but is visibly distinct from imaging "Blue"/"Red".
public class FilterColorConverter : IValueConverter
{
    public static readonly FilterColorConverter Instance = new();

    private static readonly IBrush Default = new ImmutableSolidColorBrush(Color.Parse("#8a8f99"));

    // Keyed case-insensitively as a safety net; the canonicalizer already emits a
    // stable casing per label.
    private static readonly Dictionary<string, IBrush> Palette =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Imaging — broadband
            ["L"] = Brush("#d8dde6"),
            ["Red"] = Brush("#e0524e"),
            ["Green"] = Brush("#56c073"),
            ["Blue"] = Brush("#5b8def"),
            // Imaging — narrowband
            ["Ha"] = Brush("#b3304d"),
            ["OIII"] = Brush("#1fb6a8"),
            ["SII"] = Brush("#d9a441"),
            ["Hb"] = Brush("#45b6d8"),
            ["He"] = Brush("#8e6fd0"),
            ["NII"] = Brush("#e07a3c"),
            // Imaging — one-shot-color + multiband (per-product colors)
            ["Color"] = Brush("#b89ad0"),
            ["LPro"] = Brush("#aab3c0"),
            ["LUltimate"] = Brush("#b154a6"),
            ["L-eNhance"] = Brush("#5bbf9e"),
            // Photometric (Johnson-Cousins) — shifted shades, distinct from imaging
            ["U"] = Brush("#6a4fb0"),
            ["B"] = Brush("#2f4bb0"),
            ["V"] = Brush("#9cbf3a"),
            ["R"] = Brush("#a83228"),
            ["I"] = Brush("#6e2b2b"),
        };

    private static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Palette.TryGetValue(s.Trim(), out var brush))
            return brush;

        return Default;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
