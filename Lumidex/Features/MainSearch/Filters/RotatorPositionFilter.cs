﻿using Lumidex.Core.Data;

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

    public override string ToString() => $"{DisplayName} = ({MinValue}, {MaxValue})";
}
