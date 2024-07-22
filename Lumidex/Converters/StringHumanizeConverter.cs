using Avalonia.Data;
using Avalonia.Data.Converters;
using Humanizer;
using System.Globalization;

namespace Lumidex.Converters;

public class StringHumanizeConverter : IValueConverter
{
    public static readonly StringHumanizeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return s.Humanize(LetterCasing.Title);
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidOperationException(), BindingErrorType.Error);
    }
}
