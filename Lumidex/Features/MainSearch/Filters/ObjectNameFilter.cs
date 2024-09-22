using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ObjectNameFilter : FilterViewModelBase
{
    [ObservableProperty] string? _name;

    public override string DisplayName => "Object Name";

    protected override void OnClear() => Name = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (Name is { Length: > 0 })
        {
            if (Name.StartsWith("\"") && Name.EndsWith("\""))
            {
                query = query.Where(f => f.ObjectName == Name.Substring(1, Name.Length - 2));
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

    public override string ToString() => $"{DisplayName} = {Name}";
}
