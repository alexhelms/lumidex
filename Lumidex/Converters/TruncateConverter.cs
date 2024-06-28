using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class TruncateConverter : IValueConverter
{
    public static readonly TruncateConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(parameter?.ToString() ?? "", out int length))
        {
            if (s.Length > "...".Length)
            {
                return s[..(length - "...".Length)] + "...";
            }

            return "...";
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidOperationException(), BindingErrorType.Error);
    }
}
