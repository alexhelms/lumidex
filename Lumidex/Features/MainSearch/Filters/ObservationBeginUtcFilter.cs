using Lumidex.Core.Data;
using System.Globalization;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObservationBeginUtcFilter : FilterViewModelBase
{
    [ObservableProperty]
    public partial DateTime? DateBegin { get; set; }

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

    public override PersistedFilter? Persist() => DateBegin is null
        ? null
        : new PersistedFilter
        {
            Name = "ObservationBeginUtc",
            Data = DateBegin.Value.ToString("o"),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "ObservationBeginUtc" &&
            DateTime.TryParseExact(persistedFilter.Data, "o", null, DateTimeStyles.None, out var datetime))
        {
            DateBegin = datetime;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {DateBegin}";
}
