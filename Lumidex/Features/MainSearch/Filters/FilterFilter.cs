using LinqKit;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class FilterFilter : FilterViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public FilterFilter(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    public partial string? Filter { get; set; }

    public override string DisplayName => "Filter";

    protected override void OnClear() => Filter = null;

    // AutoCompleteBox.AsyncPopulator binds to a property, not a method group directly.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> PopulateSuggestions =>
        (searchText, cancellationToken) =>
            PopulateSuggestionsAsync(_dbContextFactory, f => f.FilterName, searchText, cancellationToken);

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
