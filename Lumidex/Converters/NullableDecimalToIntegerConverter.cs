using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class NullableDecimalToIntegerConverter : IValueConverter
{
    public static readonly NullableDecimalToIntegerConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return (decimal?)null;
        }
        else if (value is int i)
        {
            return (decimal?)i;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return (int?)null;
        }
        else if (value is decimal d)
        {
            return (int?)d;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
