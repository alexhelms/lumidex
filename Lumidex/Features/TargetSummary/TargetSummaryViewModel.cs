using Lumidex.Core.Targets;
using Lumidex.Services;

namespace Lumidex.Features.TargetSummary;

public partial class TargetSummaryViewModel : ViewModelBase
{
    private readonly TargetResolutionService _resolution;
    private readonly TargetSummaryQuery _query;
    private readonly DialogService _dialogService;
    // Serializes the DB-touching work so two reloads (e.g. fast Refresh clicks) don't resolve
    // targets against the same SQLite file at once and hit SQLITE_BUSY.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Reassigned wholesale on each load; a fresh collection + one rebind avoids the ItemsControl
    // range-notification crash the codebase hits when mutating in place.
    [ObservableProperty] public partial ObservableCollectionEx<TargetRowViewModel> Targets { get; set; } = new();

    public TargetSummaryViewModel(TargetResolutionService resolution, TargetSummaryQuery query, DialogService dialogService)
    {
        _resolution = resolution;
        _query = query;
        _dialogService = dialogService;
    }

    // Resolve names, aggregate, and rebuild the rows. async Task so a DB failure awaits a dialog
    // instead of crashing the tab; preserves which targets/scopes were expanded.
    public async Task Reload()
    {
        try
        {
            var expanded = Targets.Where(t => t.IsExpanded).Select(t => t.TargetId).ToHashSet();
            var expandedScopes = Targets
                .SelectMany(t => t.Scopes.Where(s => s.IsExpanded).Select(s => (t.TargetId, s.Scope)))
                .ToHashSet();

            var rows = await Task.Run(async () =>
            {
                await _gate.WaitAsync();
                try
                {
                    _resolution.EnsureTargetsResolved();
                    var built = _query.GetTargetSummary().Select(BuildRow).ToList();
                    // Unset bars scale to the largest REAL target's hours; exclude the synthetic
                    // "(Unnamed)" pile (TargetId 0), often the biggest, so real bars don't shrink
                    // against junk. Push it onto every row so each bar computes off its own context.
                    var max = built.Where(r => r.TargetId != 0).Select(r => r.Hours).DefaultIfEmpty(0).Max();
                    foreach (var r in built)
                    {
                        r.ReferenceMax = max;
                        foreach (var s in r.Scopes)
                        {
                            s.ReferenceMax = max;
                            foreach (var f in s.Filters) f.ReferenceMax = max;
                        }
                    }
                    return built;
                }
                finally { _gate.Release(); }
            });

            foreach (var r in rows)
            {
                r.IsExpanded = expanded.Contains(r.TargetId);
                foreach (var s in r.Scopes)
                    s.IsExpanded = expandedScopes.Contains((r.TargetId, s.Scope));
            }
            Targets = new(rows.OrderByDescending(r => r.Hours));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load the target summary");
            try { await _dialogService.ShowMessageDialog("Failed to load the target summary."); }
            catch (Exception dialogEx) { Log.Error(dialogEx, "Failed to show the target summary error dialog"); }
        }
    }

    // Build a target row from the query result, ordering filters by name within each scope.
    private static TargetRowViewModel BuildRow(TargetIntegration t)
    {
        var scopes = t.Scopes
            .Select(s => new ScopeRowViewModel
            {
                Scope = s.Scope,
                Filters = s.Filters
                    .OrderBy(f => f.Filter, StringComparer.OrdinalIgnoreCase)
                    .Select(f => new FilterRowViewModel { Filter = f.Filter, Hours = f.Hours })
                    .ToList(),
            })
            .OrderBy(s => s.Scope, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new TargetRowViewModel { TargetId = t.TargetId, CanonicalName = t.CanonicalName, Scopes = scopes };
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();
        _ = Reload();
    }

    [RelayCommand]
    private Task Refresh() => Reload();
}
