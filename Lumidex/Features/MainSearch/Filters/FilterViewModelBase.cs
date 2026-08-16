using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Lumidex.Features.MainSearch.Filters;

public abstract partial class FilterViewModelBase : ValidatableViewModelBase
{
    public abstract string DisplayName { get; }
    public abstract IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query);

    [RelayCommand]
    private void Clear()
    {
        OnClear();
    }

    protected virtual void OnClear() { }

    public virtual PersistedFilter? Persist() => null;

    public virtual bool Restore(PersistedFilter persistedFilter) => false;

    /// <summary>
    /// Backing implementation for an AutoCompleteBox.AsyncPopulator: runs <paramref name="query"/> against a
    /// fresh db context, then dedupes (case-insensitively), sorts, and caps the results. Derived filters that
    /// want autocomplete expose their own <c>PopulateSuggestions</c> property that delegates here.
    /// </summary>
    protected static async Task<IEnumerable<object>> PopulateSuggestionsAsync(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        Func<LumidexDbContext, string, IQueryable<string?>> query,
        string? searchText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var results = await query(dbContext, searchText).Distinct().ToListAsync(cancellationToken);

        return results
            .Where(n => n is { Length: > 0 })
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .OrderBy(n => n)
            .Take(25)
            .Cast<object>()
            .ToList();
    }

    /// <summary>
    /// Convenience overload for the common case: suggestions are a single ImageFile string column,
    /// substring-matched with LIKE.
    /// </summary>
    protected static Task<IEnumerable<object>> PopulateSuggestionsAsync(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        Expression<Func<ImageFile, string?>> column,
        string? searchText,
        CancellationToken cancellationToken)
        => PopulateSuggestionsAsync(
            dbContextFactory,
            (dbContext, text) => dbContext.ImageFiles
                .Select(column)
                .Where(value => value != null && EF.Functions.Like(value, $"%{text}%")),
            searchText,
            cancellationToken);
}
