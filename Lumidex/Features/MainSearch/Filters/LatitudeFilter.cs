using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class LatitudeFilter : FilterViewModelBase
{
    [ObservableProperty] decimal? _minValue;
    [ObservableProperty] decimal? _maxValue;

    public override string DisplayName => "Latitude";

    protected override void OnClear()
    {
        MinValue = null;
        MaxValue = null;
    }

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (MinValue is { } min)
        {
            var minimum = (double)min;
            query = query.Where(f => f.Latitude >= minimum);
        }

        if (MaxValue is { } max)
        {
            var maximum = (double)max;
            query = query.Where(f => f.Latitude <= maximum);
        }

        return query;
    }

    public override PersistedFilter? Persist()
    {
        if (MinValue is null && MaxValue is null)
            return null;

        var min = MinValue.HasValue ? MinValue.Value.ToString() : string.Empty;
        var max = MaxValue.HasValue ? MaxValue.Value.ToString() : string.Empty;

        return new PersistedFilter
        {
            Name = "Latitude",
            Data = $"{min}|{max}"
        };
    }

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "Latitude")
        {
            var data = persistedFilter.Data ?? string.Empty;
            var split = data.Split('|', count: 2);
            if (split.Length == 2)
            {
                if (decimal.TryParse(split[0], out var min))
                {
                    MinValue = min;
                    restored = true;
                }

                if (decimal.TryParse(split[1], out var max))
                {
                    MaxValue = max;
                    restored = true;
                }
            }
        }

        return restored;
    }

    public override string ToString() => $"{DisplayName} = ({MinValue}, {MaxValue})";
}
