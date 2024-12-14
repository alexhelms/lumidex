using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ExposureFilter : FilterViewModelBase
{
    [ObservableProperty] decimal? _exposureMin;
    [ObservableProperty] decimal? _exposureMax;

    public override string DisplayName => "Exposure";

    protected override void OnClear()
    {
        ExposureMin = null;
        ExposureMax = null;
    }

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (ExposureMin is { } min)
        {
            var minimum = TimeSpan.FromSeconds((double)min);
            query = query.Where(f => f.Exposure >= minimum.TotalSeconds);
        }

        if (ExposureMax is { } max)
        {
            var maximum = TimeSpan.FromSeconds((double)max);
            query = query.Where(f => f.Exposure <= maximum.TotalSeconds);
        }

        return query;
    }

    public override PersistedFilter? Persist()
    {
        if (ExposureMin is null && ExposureMax is null)
            return null;

        var min = ExposureMin.HasValue ? ExposureMin.Value.ToString() : string.Empty;
        var max = ExposureMax.HasValue ? ExposureMax.Value.ToString() : string.Empty;

        return new PersistedFilter
        {
            Name = "Exposure",
            Data = $"{min}|{max}"
        };
    }

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "Exposure")
        {
            var data = persistedFilter.Data ?? string.Empty;
            var split = data.Split('|', count: 2);
            if (split.Length == 2)
            {
                if (decimal.TryParse(split[0], out var min))
                {
                    ExposureMin = min;
                    restored = true;
                }

                if (decimal.TryParse(split[1], out var max))
                {
                    ExposureMax = max;
                    restored = true;
                }
            }
        }

        return restored;
    }

    public override string ToString() => $"{DisplayName} = ({ExposureMin}, {ExposureMax})";
}
