using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace Lumidex.Converters;

public class LongToColorConverter : IValueConverter
{
    public static readonly TruncateConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long l)
        {
            return Color.FromUInt32((uint)l);
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
        {
            return (long)c.ToUInt32();
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
