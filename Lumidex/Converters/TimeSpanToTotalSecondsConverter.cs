using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class TimeSpanToTotalSecondsConverter : IValueConverter
{
    public static readonly TimeSpanToTotalSecondsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            return ts.TotalSeconds;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return TimeSpan.FromSeconds(d);
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
