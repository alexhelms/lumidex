using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class RightAscensionFilter : FilterViewModelBase
{
    [ObservableProperty] decimal? _minValue;
    [ObservableProperty] decimal? _maxValue;

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

    public override string ToString() => $"{DisplayName} = ({MinValue}, {MaxValue})";
}
