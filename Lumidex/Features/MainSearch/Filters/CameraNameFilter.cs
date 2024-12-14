using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class CameraNameFilter : FilterViewModelBase
{
    [ObservableProperty] string? _name;

    public override string DisplayName => "Camera Name";

    protected override void OnClear() => Name = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            query = query.Where(f => EF.Functions.Like(f.CameraName, $"%{Name}%"));
        }

        return query;
    }

    public override PersistedFilter? Persist() => Name is null
        ? null
        : new PersistedFilter
        {
            Name = "CameraName",
            Data = Name,
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "CameraName")
        {
            Name = persistedFilter.Data;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {Name}";
}
