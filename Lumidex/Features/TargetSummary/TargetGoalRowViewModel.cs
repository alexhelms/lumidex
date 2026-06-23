using Lumidex.Core.Targets;

namespace Lumidex.Features.TargetSummary;

// One colored slice of a progress bar: a filter's acquired hours. The bar's filled portion is
// these segments; the unfilled tail is the row's Remainder (the bar's "full" point − acquired).
public record BarSegment(string Filter, double Hours);

// A filter row — the ONLY level with an editable goal. When a goal is set the bar tracks
// progress toward it and the % shows; when unset, the bar instead scales to the largest target
// (ReferenceMax, an absolute "compare by data" view) and no goal/percent is shown.
public partial class FilterGoalRowViewModel : ObservableObject
{
    public required int TargetId { get; init; }
    public required string Scope { get; init; }
    public required string Filter { get; init; }
    public required double Hours { get; init; }

    // The reference for an UNSET bar (the largest target's hours); set by the VM each load.
    public double ReferenceMax { get; set; }

    // Editable explicit goal; null/0 = unset.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGoal))]
    [NotifyPropertyChangedFor(nameof(EffectiveGoal))]
    [NotifyPropertyChangedFor(nameof(BarGoal))]
    [NotifyPropertyChangedFor(nameof(PercentComplete))]
    [NotifyPropertyChangedFor(nameof(Remainder))]
    public partial double? ExplicitGoal { get; set; }

    public bool HasGoal => ExplicitGoal is > 0;
    // For roll-up: an unset filter contributes its acquired hours to the derived parent goal.
    public double EffectiveGoal => HasGoal ? ExplicitGoal!.Value : Hours;
    // The bar's "full" point: the goal when set, else the largest-target reference (absolute view).
    public double BarGoal => HasGoal ? EffectiveGoal : ReferenceMax;
    public double Remainder => Math.Max(0, BarGoal - Hours);   // unfilled tail (clamped, never negative)
    public double? PercentComplete => HasGoal ? Math.Min(100, Hours / EffectiveGoal * 100) : null;

    public IReadOnlyList<BarSegment> Segments => [new BarSegment(Filter, Hours)];

    // True when this row's bare-B/R flip partner (see FilterCanonicalizer.FlipPartnerOf) has no
    // sibling cell on the same scope — set by the VM at build. Lets a goal edit UPDATE a row
    // stored under the partner label (saved before the scope's filter context flipped) instead
    // of stranding it and inserting a twin row for the same channel.
    public bool AllowFlipPartnerMatch { get; set; }

    // Persists this filter's goal; assigned by TargetSummaryViewModel. A callback (not a binding
    // to the parent VM) because the row lives in the expander's deferred template, where an
    // ancestor/#element binding crashes Avalonia's binding-error logger.
    public Func<FilterGoalRowViewModel, Task>? PersistGoal { get; set; }

    // Re-raises the parent scope + target derived properties so their bars/goals update live
    // when this filter's goal changes, WITHOUT a full reload (set by the VM during build). The
    // full reload was collapsing the tree and stealing focus from the next goal box.
    public Action? RaiseAncestors { get; set; }

    [RelayCommand]
    private Task SetGoal() => PersistGoal?.Invoke(this) ?? Task.CompletedTask;
}

// A scope (telescope) row. Hours/Goal are derived sums of its filters; it expands to show its
// filters. HasGoal is true once any filter beneath it carries an explicit goal.
public partial class ScopeGoalRowViewModel : ObservableObject
{
    public required string Scope { get; init; }
    public required IReadOnlyList<FilterGoalRowViewModel> Filters { get; init; }
    public DateTime? First { get; init; }
    public DateTime? Last { get; init; }
    public double ReferenceMax { get; set; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    // Checked in the expanded scope list to fold this scope's telescope name into another's.
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public bool HasGoal => Filters.Any(f => f.HasGoal);
    public double Hours => Filters.Sum(f => f.Hours);
    public double Goal => Filters.Sum(f => f.EffectiveGoal);
    public double BarGoal => HasGoal ? Goal : ReferenceMax;
    public double Remainder => Math.Max(0, BarGoal - Hours);
    public double? PercentComplete => HasGoal ? Math.Min(100, Hours / Goal * 100) : null;
    public IReadOnlyList<BarSegment> Segments => Filters.Select(f => new BarSegment(f.Filter, f.Hours)).ToList();

    // Re-raise the goal-derived properties (the bar + numbers) after a child filter's goal
    // changes — an in-memory live update instead of rebuilding the row. Hours/Segments are
    // unaffected by a goal edit, so they're not raised.
    public void RaiseDerived()
    {
        OnPropertyChanged(nameof(HasGoal));
        OnPropertyChanged(nameof(Goal));
        OnPropertyChanged(nameof(BarGoal));
        OnPropertyChanged(nameof(Remainder));
        OnPropertyChanged(nameof(PercentComplete));
    }
}

// A canonical target row. A single-scope target hides the scope layer (CanExpand false) and
// shows its filters directly; multi-scope targets expand into independently-expandable scopes.
public partial class TargetGoalRowViewModel : ObservableObject
{
    public required int TargetId { get; init; }
    public required string CanonicalName { get; init; }
    public required IReadOnlyList<ScopeGoalRowViewModel> Scopes { get; init; }
    public DateTime? First { get; init; }
    public DateTime? Last { get; init; }
    public double ReferenceMax { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial bool IsExpanded { get; set; }

    public bool HasGoal => Scopes.Any(s => s.HasGoal);
    public double Hours => Scopes.Sum(s => s.Hours);
    public double Goal => Scopes.Sum(s => s.Goal);
    public double BarGoal => HasGoal ? Goal : ReferenceMax;
    public double Remainder => Math.Max(0, BarGoal - Hours);
    public double? PercentComplete => HasGoal ? Math.Min(100, Hours / Goal * 100) : null;

    // Only multi-scope targets expand into a scope layer; single-scope shows filters directly.
    public bool CanExpand => Scopes.Count > 1;
    public IReadOnlyList<FilterGoalRowViewModel> Filters => Scopes.Count == 1 ? Scopes[0].Filters : [];

    // Target-level bar segments: each canonical filter's hours summed across scopes, in the
    // classifier's order so the colored bar reads in the standard filter sequence.
    public IReadOnlyList<BarSegment> Segments => Scopes
        .SelectMany(s => s.Filters)
        .GroupBy(f => f.Filter)
        .Select(g => new BarSegment(g.Key, g.Sum(f => f.Hours)))
        .OrderBy(b => FilterClassifier.SortKey(b.Filter))
        .ToList();

    // Live in-memory update of the goal-derived properties after a descendant filter's goal
    // changes (Hours/Segments unaffected by a goal edit, so not raised).
    public void RaiseDerived()
    {
        OnPropertyChanged(nameof(HasGoal));
        OnPropertyChanged(nameof(Goal));
        OnPropertyChanged(nameof(BarGoal));
        OnPropertyChanged(nameof(Remainder));
        OnPropertyChanged(nameof(PercentComplete));
    }
}
