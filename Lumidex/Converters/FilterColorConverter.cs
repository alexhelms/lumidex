using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Globalization;

namespace Lumidex.Converters;

// Maps a filter label to its bar-segment color. Any label not in the palette (including the
// "(No filter)" bucket and genuinely unknown filters) falls back to a neutral gray so every
// segment still renders. Returns an IBrush so it binds straight to a Rectangle Fill.
//
// The palette covers the common imaging filters (Luminance, RGB, the SHO + extra narrowbands, OSC
// "Color", multiband) and the Johnson-Cousins photometric letters (U B V R I) in shifted shades of
// the same hue family, so a photometric "B"/"R" reads as blue/red but distinct from imaging
// "Blue"/"Red". Folding filter synonyms ("H" -> "Ha") onto one label is a separate concern.
public class FilterColorConverter : IValueConverter
{
    public static readonly FilterColorConverter Instance = new();

    private static readonly IBrush Default = new ImmutableSolidColorBrush(Color.Parse("#8a8f99"));

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
            // Imaging — one-shot-color + multiband
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
