using System.ComponentModel;

namespace Lumidex.Core.Data;

public class AppSettings
{
    public int Id { get; set; }

    public bool PersistFiltersOnExit { get; set; } = true;

    public ICollection<AstrobinFilter> AstrobinFilters { get; set; } = [];

    public ICollection<PersistedFilter> PersistedFilters { get; set; } = [];
}
