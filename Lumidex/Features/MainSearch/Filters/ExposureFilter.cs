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
            query = query.Where(f => f.Exposure!.Value >= minimum.TotalSeconds);
        }

        if (ExposureMax is { } max)
        {
            var maximum = TimeSpan.FromSeconds((double)max);
            query = query.Where(f => f.Exposure!.Value <= maximum.TotalSeconds);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = ({ExposureMin}, {ExposureMax})";
}
