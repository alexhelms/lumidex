using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class LocalDateTimeConverter : IValueConverter
{
    public static readonly LocalDateTimeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            return dt.ToLocalTime();
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            return dt.ToUniversalTime();
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
