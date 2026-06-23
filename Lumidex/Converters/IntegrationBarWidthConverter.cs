using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

// Computes a filter segment's pixel width for the target integration bar.
// Inputs (MultiBinding order): [segment hours, row GoalHours, parent MaxTotalHours,
// track pixel width]. The denominator is the goal when one is set, otherwise the
// largest target's total (the "relative to your biggest target" fallback from the
// spec). Width = (hours / denominator) * trackWidth, clamped to the track so a
// target that overshoots its goal can't paint past the bar. The unfilled remainder
// is the track Border's own background showing through behind the segments, so no
// explicit "empty tail" element is needed.
public class IntegrationBarWidthConverter : IMultiValueConverter
{
    public static readonly IntegrationBarWidthConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Bindings can deliver UnsetValue mid-layout; treat anything non-numeric as 0.
        double hours = AsDouble(values.ElementAtOrDefault(0));
        double? goal = AsNullableDouble(values.ElementAtOrDefault(1));
        double max = AsDouble(values.ElementAtOrDefault(2));
        double trackWidth = AsDouble(values.ElementAtOrDefault(3));

        double denominator = goal is > 0 ? goal.Value : max;
        if (denominator <= 0 || trackWidth <= 0)
            return 0d;

        double width = hours / denominator * trackWidth;
        return Math.Clamp(width, 0d, trackWidth);
    }

    private static double AsDouble(object? value) => value is double d ? d : 0d;

    private static double? AsNullableDouble(object? value) => value as double?;
}
