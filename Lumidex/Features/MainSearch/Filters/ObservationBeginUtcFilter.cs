using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObservationBeginUtcFilter : FilterViewModelBase
{
    [ObservableProperty] DateTime? _dateBegin;

    public override string DisplayName => "Date Begin UTC";

    protected override void OnClear() => DateBegin = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (DateBegin is { } dateBegin)
        {
            query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = {DateBegin}";
}
