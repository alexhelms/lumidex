using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObservationEndFilter : FilterViewModelBase
{
    [ObservableProperty] DateTime? _dateEnd;

    public override string DisplayName => "Date End";

    protected override void OnClear() => DateEnd = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (DateEnd is { } dateEnd)
        {
            query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = {DateEnd}";
}
