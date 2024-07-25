using Avalonia.Data.Converters;
using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Converters;

public static class FuncConverters
{
    public static readonly IValueConverter EqualToOne =
        new FuncValueConverter<int, bool>(value => value == 1);

    public static readonly IValueConverter ContainsFilter =
        new FuncValueConverter<IList<FilterViewModelBase>, Type, bool>((items, type) =>
        {
            if (items is not null && type is not null)
            {
                return items.Select(x => x.GetType()).ToHashSet().Contains(type);
            }

            return false;
        });

    public static readonly IValueConverter DegreesToHours =
        new FuncValueConverter<double?, double?>(value => value.HasValue ? value / 15.0 : null);
}
