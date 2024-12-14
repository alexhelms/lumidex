using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class RightAscensionFilter : FilterViewModelBase
{
    [ObservableProperty]
    public partial decimal? MinValue { get; set; }

    [ObservableProperty]
    public partial decimal? MaxValue { get; set; }

    public override string DisplayName => "Right Ascension";

    protected override void OnClear()
    {
        MinValue = null;
        MaxValue = null;
    }

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (MinValue is { } min)
        {
            // Convert hours to degrees
            var minimum = (double)min * 15.0;
            query = query.Where(f => f.RightAscension >= minimum);
        }

        if (MaxValue is { } max)
        {
            // Convert hours to degrees
            var maximum = (double)max * 15.0;
            query = query.Where(f => f.RightAscension <= maximum);
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
            Name = "RightAscension",
            Data = $"{min}|{max}"
        };
    }

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "RightAscension")
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
