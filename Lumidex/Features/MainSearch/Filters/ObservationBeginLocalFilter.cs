using Lumidex.Core.Data;
using System.Globalization;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObservationBeginLocalFilter : FilterViewModelBase
{
    [ObservableProperty] DateTime? _dateBegin;

    public override string DisplayName => "Date Begin Local";

    protected override void OnClear() => DateBegin = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (DateBegin is { } dateBegin)
        {
            query = query.Where(f => f.ObservationTimestampLocal >= dateBegin);
        }

        return query;
    }

    public override PersistedFilter? Persist() => DateBegin is null
        ? null
        : new PersistedFilter
        {
            Name = "ObservationBeginLocal",
            Data = DateBegin.Value.ToString("o"),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "ObservationBeginLocal" &&
            DateTime.TryParseExact(persistedFilter.Data, "o", null, DateTimeStyles.None, out var datetime))
        {
            DateBegin = datetime;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {DateBegin}";
}
