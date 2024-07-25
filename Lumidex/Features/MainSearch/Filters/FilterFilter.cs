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
            query = query.Where(f => f.FilterName == filter);
        }

        return query;
    }

    public override string ToString() => $"{DisplayName} = {Filter}";
}
