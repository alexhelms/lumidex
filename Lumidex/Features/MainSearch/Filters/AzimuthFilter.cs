using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class AzimuthFilter : FilterViewModelBase
{
    [ObservableProperty] decimal? _minValue;
    [ObservableProperty] decimal? _maxValue;

    public override string DisplayName => "Azimuth";

    protected override void OnClear()
    {
        MinValue = null;
        MaxValue = null;
    }

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        var minimum = (double?)(MinValue is { } min ? min : 0);
        var maximum = (double?)MaxValue is { } max ? max : double.MaxValue;

        if (minimum > maximum)
        {
            query = query.Where(f => f.Azimuth >= minimum || f.Azimuth <= maximum);
        }
        else
        {
            if (minimum > 0)
            {
                query = query.Where(f => f.Azimuth >= minimum);
            }

            if (maximum < double.MaxValue)
            {
                query = query.Where(f => f.Azimuth <= maximum);
            }
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = ({MinValue}, {MaxValue})";
}
