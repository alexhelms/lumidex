using Avalonia.Data.Converters;
using Avalonia.Data;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Lumidex.Converters;

public class ColorToBrushConverter : IValueConverter
{
    public static readonly ColorToBrushConverter Instance = new();

    private static Dictionary<Color, IBrush> _brushCache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            if (!_brushCache.TryGetValue(color, out IBrush? brush))
            {
                brush = new SolidColorBrush(color).ToImmutable();
                _brushCache[color] = brush;
            }

            return brush;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ImmutableSolidColorBrush immutableBrush)
        {
            return immutableBrush.Color;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}