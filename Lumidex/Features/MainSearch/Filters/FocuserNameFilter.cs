using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class FocuserNameFilter : FilterViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public FocuserNameFilter(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public override string DisplayName => "Focuser Name";

    protected override void OnClear() => Name = null;

    // AutoCompleteBox.AsyncPopulator binds to a property, not a method group directly.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> PopulateSuggestions =>
        (searchText, cancellationToken) =>
            PopulateSuggestionsAsync(_dbContextFactory, f => f.FocuserName, searchText, cancellationToken);

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            query = query.Where(f => EF.Functions.Like(f.FocuserName, $"%{Name}%"));
        }

        return query;
    }

    public override PersistedFilter? Persist() => Name is null
        ? null
        : new PersistedFilter
        {
            Name = "FocuserName",
            Data = Name,
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "FocuserName")
        {
            Name = persistedFilter.Data;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Name}";
}
