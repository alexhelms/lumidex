using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObservationEndLocalFilter : FilterViewModelBase
{
    [ObservableProperty] DateTime? _dateEnd;

    public override string DisplayName => "Date End Local";

    protected override void OnClear() => DateEnd = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (DateEnd is { } dateEnd)
        {
            // Set the time to the last possible moment before the next day
            // so the search is inclusive of the same day.
            dateEnd = new DateTime(dateEnd.Year, dateEnd.Month, dateEnd.Day, 23, 59, 59, 999);
            query = query.Where(f => f.ObservationTimestampLocal <= dateEnd);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = {DateEnd}";
}
