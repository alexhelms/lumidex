using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObjectNameFilter : FilterViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public ObjectNameFilter(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public override string DisplayName => "Object Name";

    protected override void OnClear() => Name = null;

    // AutoCompleteBox.AsyncPopulator binds to a property, not a method group directly.
    // ObjectName suggestions draw from two sources (ImageFiles and ObjectAliases), so this uses the
    // general query-delegate overload rather than the single-column convenience overload.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> PopulateSuggestions =>
        (searchText, cancellationToken) => PopulateSuggestionsAsync(
            _dbContextFactory,
            (dbContext, text) => dbContext.ImageFiles
                .Where(f => f.ObjectName != null && EF.Functions.Like(f.ObjectName, $"%{text}%"))
                .Select(f => f.ObjectName)
                .Concat(dbContext.ObjectAliases
                    .Where(a => EF.Functions.Like(a.Alias, $"%{text}%"))
                    .Select(a => a.Alias)),
            searchText,
            cancellationToken);

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            if (Name.StartsWith('"') && Name.EndsWith('"'))
            {
                var objectName = Name[1..^1];
                query = query.Where(f => f.ObjectName == objectName);
            }
            else 
            { 
                // Create a set that contains the user's filter, any matching alias, and any matching object name
                var aliases = new HashSet<string>([Name], StringComparer.InvariantCultureIgnoreCase);

                aliases.UnionWith(dbContext.ObjectAliases
                    .Where(a => EF.Functions.Like(a.Alias, $"%{Name}%"))
                    .Select(a => a.ObjectName));

                aliases.UnionWith(dbContext.ImageFiles
                    .Where(f => f.ObjectName != null)
                    .Select(f => f.ObjectName)
                    .Where(objectName => EF.Functions.Like(objectName, $"%{Name}%"))!);

                query = query
                    .Where(f => f.ObjectName != null)
                    .Where(f => aliases.Contains(f.ObjectName!));
            
            }
        }

        return query;
    }

    public override PersistedFilter? Persist() => Name is null
        ? null
        : new PersistedFilter
        {
            Name = "ObjectName",
            Data = Name,
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "ObjectName")
        {
            Name = persistedFilter.Data;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Name}";
}
