namespace Lumidex.Core.Data;

public class AppSettings
{
    public int Id { get; set; }

    public bool PersistFiltersOnExit { get; set; } = true;

    public bool UseIntermediateFramesForPlots { get; set; }

    public ICollection<AstrobinFilter> AstrobinFilters { get; set; } = [];

    public ICollection<PersistedFilter> PersistedFilters { get; set; } = [];
}
