namespace Lumidex.Features.TargetSummary;

// One colored slice of a bar: a filter's acquired hours.
public record BarSegment(string Filter, double Hours);

// A filter row — the leaf of the tree. The bar's filled portion is its hours; the unfilled tail
// scales the row to the largest target (ReferenceMax) for a "share of the largest" comparison.
public class FilterRowViewModel
{
    public required string Filter { get; init; }
    public required double Hours { get; init; }
    public double ReferenceMax { get; set; }
    public double Remainder => Math.Max(0, ReferenceMax - Hours);
    public IReadOnlyList<BarSegment> Segments => [new BarSegment(Filter, Hours)];
}

// A scope (telescope) row. Hours is the sum of its filters; it expands to show them.
public partial class ScopeRowViewModel : ObservableObject
{
    public required string Scope { get; init; }
    public required IReadOnlyList<FilterRowViewModel> Filters { get; init; }
    public double ReferenceMax { get; set; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    public double Hours => Filters.Sum(f => f.Hours);
    public double Remainder => Math.Max(0, ReferenceMax - Hours);
    public IReadOnlyList<BarSegment> Segments => Filters.Select(f => new BarSegment(f.Filter, f.Hours)).ToList();
}

// A canonical target row. A single-scope target hides the scope layer (CanExpand false) and shows
// its filters directly; multi-scope targets expand into independently-expandable scopes.
public partial class TargetRowViewModel : ObservableObject
{
    public required int TargetId { get; init; }
    public required string CanonicalName { get; init; }
    public required IReadOnlyList<ScopeRowViewModel> Scopes { get; init; }
    public double ReferenceMax { get; set; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    public double Hours => Scopes.Sum(s => s.Hours);
    public double Remainder => Math.Max(0, ReferenceMax - Hours);

    public bool CanExpand => Scopes.Count > 1;
    public IReadOnlyList<FilterRowViewModel> Filters => Scopes.Count == 1 ? Scopes[0].Filters : [];

    // Target-level bar segments: each filter's hours summed across the target's scopes.
    public IReadOnlyList<BarSegment> Segments => Scopes
        .SelectMany(s => s.Filters)
        .GroupBy(f => f.Filter)
        .Select(g => new BarSegment(g.Key, g.Sum(f => f.Hours)))
        .ToList();
}
