using LinqKit;
using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class FilterFilter : FilterViewModelBase
{
    [ObservableProperty] string? _filter;

    public override string DisplayName => "Filter";

    protected override void OnClear() => Filter = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Filter is { Length: > 0 } filter)
        {
            var items = filter.Split('|');
            if (items.Length > 0)
            {
                var predicate = PredicateBuilder.New<ImageFile>();

                foreach (var item in items)
                {
                    string temp = item;
                    predicate.Or(f => f.FilterName == temp);
                }

                query = query.Where(predicate);
            }
            else
            {
                query = query.Where(f => f.FilterName == filter);
            }
        }

        return query;
    }

    public override PersistedFilter? Persist() => Filter is null
        ? null
        : new PersistedFilter
        {
            Name = "Filter",
            Data = Filter,
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "Filter")
        {
            Filter = persistedFilter.Data;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Filter}";
}
