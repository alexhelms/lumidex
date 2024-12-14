using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class RotatorPositionFilter : FilterViewModelBase
{
    [ObservableProperty] decimal? _minValue;
    [ObservableProperty] decimal? _maxValue;

    public override string DisplayName => "Rotator Position";

    protected override void OnClear()
    {
        MinValue = null;
        MaxValue = null;
    }

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        double minimum = MinValue.HasValue ? (double)MinValue.Value : double.MinValue;
        double maximum = MaxValue.HasValue ? (double)MaxValue.Value : double.MaxValue;

        if (minimum > maximum)
        {
            query = query.Where(f => f.RotatorPosition >= minimum || f.RotatorPosition <= maximum);
        }
        else
        {
            if (minimum > 0)
            {
                query = query.Where(f => f.RotatorPosition >= minimum);
            }

            if (maximum < double.MaxValue)
            {
                query = query.Where(f => f.RotatorPosition <= maximum);
            }
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
            Name = "RotatorPosition",
            Data = $"{min}|{max}"
        };
    }

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "RotatorPosition")
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
