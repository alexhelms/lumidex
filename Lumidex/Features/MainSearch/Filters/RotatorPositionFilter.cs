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
        if (MinValue is { } min)
        {
            var minimum = (double)min;
            query = query.Where(f => f.RotatorPosition >= minimum);
        }

        if (MaxValue is { } max)
        {
            var maximum = (double)max;
            query = query.Where(f => f.RotatorPosition <= maximum);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = ({MinValue}, {MaxValue})";
}
