using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class TelescopeNameFilter : FilterViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public TelescopeNameFilter(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public override string DisplayName => "Telescope Name";

    protected override void OnClear() => Name = null;

    // AutoCompleteBox.AsyncPopulator binds to a property, not a method group directly.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> PopulateSuggestions =>
        (searchText, cancellationToken) =>
            PopulateSuggestionsAsync(_dbContextFactory, f => f.TelescopeName, searchText, cancellationToken);

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            query = query.Where(f => EF.Functions.Like(f.TelescopeName, $"%{Name}%"));
        }

        return query;
    }

    public override PersistedFilter? Persist() => Name is null
        ? null
        : new PersistedFilter
        {
            Name = "TelescopeName",
            Data = Name,
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "TelescopeName")
        {
            Name = persistedFilter.Data;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Name}";
}
