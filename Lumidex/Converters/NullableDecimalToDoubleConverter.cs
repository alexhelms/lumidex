using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class NullableDecimalToDoubleConverter : IValueConverter
{
    public static readonly NullableDecimalToDoubleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return (decimal?)null;
        }
        else if (value is double d)
        {
            return (decimal?)d;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return (double?)null;
        }
        else if (value is decimal d)
        {
            return (double?)d;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
