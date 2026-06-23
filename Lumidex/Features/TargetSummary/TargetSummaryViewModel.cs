using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Services;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.TargetSummary;

// The per-layer sort keys (the dropdown). Filter rows are exempt — they always use the
// FilterClassifier's static order.
public enum TargetSummarySort { Alphabetical, DataAcquired, DataNeeded, Goal, FirstAcquired, LastAcquired }

public partial class TargetSummaryViewModel : ViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    private readonly TargetResolutionService _resolution;
    private readonly TargetGoalQuery _query;
    private readonly DialogService _dialogService;
    // Serializes the DB-writing paths (Reload's resolve, a goal write, MergeSelected) so a
    // background reload and a user edit don't hit the same SQLite file at once → SQLITE_BUSY.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _loaded;   // gate the sort-change reload until the first activation has run

    // Reassigned wholesale on each load (the codebase's bulk-replace pattern); a fresh
    // collection + one rebind avoids the ItemsControl range-notification crash.
    [ObservableProperty] public partial ObservableCollectionEx<TargetGoalRowViewModel> Targets { get; set; } = new();

    // The active user merges, for the "Manage merges" flyout. Rebuilt each reload (so it reflects a
    // just-made merge or an undo) alongside Targets.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoMerges))]
    public partial ObservableCollectionEx<MergeOperationRowViewModel> MergeOperations { get; set; } = new();

    // Drives the flyout's "No merges yet." empty state; re-raised when MergeOperations is reassigned.
    public bool HasNoMerges => MergeOperations.Count == 0;

    // The sort control. Changing either re-sorts every layer (filters stay static). Default is
    // Alphabetical ascending; persisting the choice between sessions is a follow-up.
    [ObservableProperty] public partial TargetSummarySort SortBy { get; set; } = TargetSummarySort.DataAcquired;
    [ObservableProperty] public partial bool SortAscending { get; set; } = false;

    public IReadOnlyList<TargetSummarySort> SortOptions { get; } = Enum.GetValues<TargetSummarySort>();

    public TargetSummaryViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        TargetResolutionService resolution,
        TargetGoalQuery query,
        DialogService dialogService)
    {
        _dbContextFactory = dbContextFactory;
        _resolution = resolution;
        _query = query;
        _dialogService = dialogService;
    }

    partial void OnSortByChanged(TargetSummarySort value) { if (_loaded) _ = Reload(); }
    partial void OnSortAscendingChanged(bool value) { if (_loaded) _ = Reload(); }

    // Re-resolve names, query the goal tree, and rebuild the rows. async Task so a DB failure
    // awaits a dialog instead of crashing the tab; preserves which targets were expanded.
    public async Task Reload()
    {
        try
        {
            var expanded = Targets.Where(t => t.IsExpanded).Select(t => t.TargetId).ToHashSet();
            var expandedScopes = Targets
                .SelectMany(t => t.Scopes.Where(s => s.IsExpanded).Select(s => (t.TargetId, s.Scope)))
                .ToHashSet();
            var (by, asc) = (SortBy, SortAscending);

            var (rows, merges) = await Task.Run(async () =>
            {
                await _gate.WaitAsync();
                try
                {
                    _resolution.EnsureTargetsResolved();
                    var built = _query.GetTargetGoals().Select(t => BuildRow(t, by, asc)).ToList();
                    // Unset bars scale to the largest REAL target's hours (the "% of largest"
                    // view); exclude the synthetic "(Unnamed)" pile (TargetId 0), often the
                    // biggest, so real bars don't shrink against junk. Push the reference onto
                    // every row so each bar computes it off its own DataContext (no ancestor
                    // binding in the deferred templates).
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
                    // The active merges for the flyout, built under the same gate/read.
                    var merges = _resolution.GetMergeOperations()
                        .Select(o => new MergeOperationRowViewModel
                        {
                            OperationId = o.OperationId,
                            Kind = o.Kind,
                            Label = $"{string.Join(", ", o.AbsorbedLabels)} → {o.SurvivorLabel}",
                            UndoAction = UndoMergeRow,
                        })
                        .ToList();
                    return (built, merges);
                }
                finally { _gate.Release(); }
            });

            foreach (var r in rows)
            {
                r.IsExpanded = expanded.Contains(r.TargetId);
                foreach (var s in r.Scopes)
                    s.IsExpanded = expandedScopes.Contains((r.TargetId, s.Scope));
            }
            Targets = new(Order(rows, by, asc));
            MergeOperations = new(merges);
            _loaded = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load the target summary");
            try { await _dialogService.ShowMessageDialog("Failed to load the target summary."); }
            catch (Exception dialogEx) { Log.Error(dialogEx, "Failed to show the target summary error dialog"); }
        }
    }

    // Build a target row from the query, wiring each filter's goal persistence, ordering the
    // filters by the classifier (static) and the scopes by the active sort.
    private TargetGoalRowViewModel BuildRow(TargetGoal t, TargetSummarySort by, bool asc)
    {
        var scopes = Order(
            t.Scopes.Select(s =>
            {
                // The scope's rendered filter labels: a goal edit may only match a row stored
                // under the bare-B/R flip partner when that partner has no cell of its own here
                // (mirrors the read-side guard in TargetGoalQuery.LookupGoal).
                var cellLabels = s.Filters.Select(f => f.Filter).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new ScopeGoalRowViewModel
                {
                    Scope = s.Scope,
                    First = s.First,
                    Last = s.Last,
                    Filters = s.Filters
                        .OrderBy(f => FilterClassifier.SortKey(f.Filter))
                        .Select(f => new FilterGoalRowViewModel
                        {
                            TargetId = t.TargetId,
                            Scope = s.Scope,
                            Filter = f.Filter,
                            Hours = f.Hours,
                            ExplicitGoal = f.ExplicitGoal,
                            PersistGoal = PersistFilterGoal,
                            AllowFlipPartnerMatch = FilterCanonicalizer.FlipPartnerOf(f.Filter) is { } p && !cellLabels.Contains(p),
                        })
                        .ToList(),
                };
            }),
            by, asc, s => s.Scope, s => s.Hours, s => s.Goal, s => s.Remainder, s => s.First, s => s.Last);

        var target = new TargetGoalRowViewModel
        {
            TargetId = t.TargetId,
            CanonicalName = t.CanonicalName,
            First = t.First,
            Last = t.Last,
            Scopes = scopes,
        };
        // Each filter recomputes its scope + target bars in memory on a goal edit (live, no reload).
        foreach (var s in scopes)
            foreach (var f in s.Filters)
                f.RaiseAncestors = () => { s.RaiseDerived(); target.RaiseDerived(); };
        return target;
    }

    // Generic per-layer ordering. Date keys push undated rows last (ascending) via MaxValue.
    private static List<T> Order<T>(IEnumerable<T> rows, TargetSummarySort by, bool asc,
        Func<T, string> name, Func<T, double> hours, Func<T, double> goal, Func<T, double> needed,
        Func<T, DateTime?> first, Func<T, DateTime?> last)
    {
        Func<T, IComparable> key = by switch
        {
            TargetSummarySort.DataAcquired => r => hours(r),
            TargetSummarySort.DataNeeded => r => needed(r),
            TargetSummarySort.Goal => r => goal(r),
            TargetSummarySort.FirstAcquired => r => first(r) ?? DateTime.MaxValue,
            TargetSummarySort.LastAcquired => r => last(r) ?? DateTime.MaxValue,
            _ => r => name(r),
        };
        // Alphabetical compares case-insensitively; the others compare their numeric/date key.
        var ordered = by == TargetSummarySort.Alphabetical
            ? rows.OrderBy(name, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(key);
        return (asc ? ordered : ordered.Reverse()).ToList();
    }

    private static List<TargetGoalRowViewModel> Order(IEnumerable<TargetGoalRowViewModel> rows, TargetSummarySort by, bool asc)
        => Order(rows, by, asc, r => r.CanonicalName, r => r.Hours, r => r.Goal, r => r.Remainder, r => r.First, r => r.Last);

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();
        _ = Reload();
    }

    [RelayCommand]
    private Task Refresh() => Reload();

    // Wired into each filter row; persists an inline goal edit then reloads so the rolled-up
    // scope/target bars reflect it. The reload preserves expansion.
    private async Task PersistFilterGoal(FilterGoalRowViewModel filter)
    {
        // Update the scope/target bars live, in memory — NO full reload, so the tree doesn't
        // collapse, focus stays in the next goal box, and the sort order doesn't shuffle while
        // editing. The DB write persists it; the next real reload re-reads from there.
        filter.RaiseAncestors?.Invoke();
        await UpsertFilterGoal(filter.TargetId, filter.Scope, filter.Filter, filter.ExplicitGoal, filter.AllowFlipPartnerMatch);
    }

    // Upsert one per-(target, scope, filter) goal. Gated + try/caught (a LostFocus edit on the
    // UI thread mustn't crash the tab on SQLITE_BUSY). A zero/negative/null goal deletes.
    //
    // The row search matches every stored row this CELL DISPLAYS, not just rows under the
    // cell's literal key: the read side (TargetGoalQuery) resolves non-destructive merges — a
    // goal saved under an absorbed scope name or absorbed target id renders on the survivor's
    // cell — and falls back to the bare-B/R flip partner. If the write side searched only the
    // literal key, clearing a displayed goal would silently no-op (the row hides under its
    // pre-merge key and resurrects on the next reload) and editing would fork a twin row
    // instead of updating the original.
    private async Task UpsertFilterGoal(int targetId, string scope, string filter, double? goalHours, bool matchFlipPartner)
    {
        await _gate.WaitAsync();
        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            // Same merge resolution the query applies before its goal join. The goals table is
            // tens of rows (one per explicit goal), so a full tracked read beats expressing
            // merge resolution in SQL.
            var scopeMap = MergeResolver.BuildScopeMap(db.ScopeMerges.AsNoTracking().ToList());
            var targetMap = MergeResolver.BuildTargetMap(db.TargetMerges.AsNoTracking().ToList());
            string MapScope(string s) => scopeMap.TryGetValue(s.ToLowerInvariant(), out var canon) ? canon : s;
            int MapTarget(int id) => id != 0 && targetMap.TryGetValue(id, out var sid) ? sid : id;
            var partner = matchFlipPartner ? FilterCanonicalizer.FlipPartnerOf(filter) : null;
            bool LabelMatches(string f) => string.Equals(f, filter, StringComparison.OrdinalIgnoreCase)
                || (partner is not null && string.Equals(f, partner, StringComparison.OrdinalIgnoreCase));
            var matches = db.TargetFilterGoals
                .AsEnumerable()
                .Where(g => MapTarget(g.TargetId) == targetId
                         && string.Equals(MapScope(g.Scope), scope, StringComparison.OrdinalIgnoreCase)
                         && LabelMatches(g.Filter))
                .ToList();

            if (goalHours is > 0)
            {
                // Prefer the row already stored under the cell's exact key; else reuse the
                // first match (a partner-labelled or pre-merge row). A kept non-exact row is
                // re-keyed to the rendered cell so it stops hiding; any other matches are
                // strands a merge made equivalent — removed in the SAME SaveChanges, so the
                // unique (TargetId, Scope, Filter) index never sees a transient duplicate
                // (the same batching AbsorbInto relies on). No same-batch key collision is
                // possible: only the kept row is re-keyed, and an exact-key row is always
                // preferred as the kept row.
                var keep = matches.FirstOrDefault(g => g.TargetId == targetId
                                                    && string.Equals(g.Scope, scope, StringComparison.OrdinalIgnoreCase)
                                                    && string.Equals(g.Filter, filter, StringComparison.OrdinalIgnoreCase))
                    ?? matches.FirstOrDefault();
                if (keep is null)
                {
                    db.TargetFilterGoals.Add(new TargetFilterGoal { TargetId = targetId, Scope = scope, Filter = filter, GoalHours = goalHours.Value });
                }
                else
                {
                    keep.GoalHours = goalHours.Value;
                    keep.TargetId = targetId;
                    keep.Scope = scope;
                    keep.Filter = filter;   // also normalizes casing on an exact match
                    foreach (var extra in matches.Where(m => m != keep))
                        db.TargetFilterGoals.Remove(extra);
                }
            }
            else
            {
                // Clearing removes EVERY row the cell displayed — deleting just one would let
                // a stranded partner/pre-merge row resurrect the goal on the next reload.
                db.TargetFilterGoals.RemoveRange(matches);
            }
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save the goal for target {TargetId} scope {Scope} filter {Filter}", targetId, scope, filter);
            await _dialogService.ShowMessageDialog("Failed to save the goal.");
        }
        finally { _gate.Release(); }
    }

    // Merge the currently-selected rows into one target.
    [RelayCommand]
    private async Task MergeSelected()
    {
        var selected = Targets.Where(t => t.IsSelected && t.TargetId != 0).ToList();
        if (selected.Count < 2)
        {
            await _dialogService.ShowMessageDialog("Select at least two targets to merge.");
            return;
        }

        // The most-integrated row is the survivor and supplies the name; ThenBy name breaks
        // equal-total ties deterministically.
        var ordered = selected.OrderByDescending(t => t.Hours).ThenBy(t => t.CanonicalName).ToList();
        var canonical = ordered[0].CanonicalName;
        try
        {
            await _gate.WaitAsync();
            try { _resolution.MergeTargets(ordered.Select(t => t.TargetId).ToList(), canonical); }
            finally { _gate.Release(); }
            await Reload();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to merge the selected targets");
            await _dialogService.ShowMessageDialog("Failed to merge the selected targets.");
        }
    }

    // Fold the selected scope rows (telescope-name variants) into one. A scope merge is GLOBAL — it
    // folds the name everywhere it appears — so it gathers selected scopes across every target. The
    // most-integrated spelling is the survivor; the rest fold into it. Non-destructive (a record
    // resolved at query time), reversible from the Manage-merges flyout.
    [RelayCommand]
    private async Task MergeScopes()
    {
        var selected = Targets.SelectMany(t => t.Scopes).Where(s => s.IsSelected).ToList();
        var names = selected.Select(s => s.Scope).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count < 2)
        {
            await _dialogService.ShowMessageDialog("Select at least two scope rows to merge.");
            return;
        }

        // Survivor = the most-integrated spelling (summed across its selected rows); ThenBy name
        // breaks ties deterministically.
        var canonical = selected
            .GroupBy(s => s.Scope, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Sum(s => s.Hours))
            .ThenBy(g => g.Key)
            .First().Key;
        var absorbed = names.Where(n => !string.Equals(n, canonical, StringComparison.OrdinalIgnoreCase)).ToList();

        try
        {
            await _gate.WaitAsync();
            try { _resolution.MergeScopes(canonical, absorbed); }
            finally { _gate.Release(); }
            await Reload();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to merge the selected scopes");
            await _dialogService.ShowMessageDialog("Failed to merge the selected scopes.");
        }
    }

    // Reverse one merge (scope or target) from the Manage-merges flyout — remove its records and
    // reload, so the folded scopes/targets reappear. Assigned to each MergeOperationRowViewModel.
    private async Task UndoMergeRow(MergeOperationRowViewModel row)
    {
        try
        {
            await _gate.WaitAsync();
            try { _resolution.UndoMerge(row.OperationId); }
            finally { _gate.Release(); }
            await Reload();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to undo merge {OperationId}", row.OperationId);
            await _dialogService.ShowMessageDialog("Failed to undo the merge.");
        }
    }
}
