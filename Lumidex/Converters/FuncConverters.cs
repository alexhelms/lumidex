using Avalonia.Data.Converters;

namespace Lumidex.Converters;

public static class FuncConverters
{
    public static readonly IValueConverter EqualToOne =
        new FuncValueConverter<int, bool>(value => value == 1);
}
