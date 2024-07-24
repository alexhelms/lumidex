using Lumidex.Core.Data;

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
}
