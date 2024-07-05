using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Lumidex.Converters;

public class StringToColorConverter : IValueConverter
{
    public static readonly StringToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && Color.TryParse(s, out var color))
        {
            return color;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
        {
            return c.ToString();
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
