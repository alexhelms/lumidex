using Avalonia.Data;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Lumidex.Converters;

public class FilenameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            try
            {
                return Path.GetFileName(s);
            }
            catch { }
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidOperationException(), BindingErrorType.Error);
    }
}
