using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Core.Targets;

// One filter's integration on one scope. First/Last are the earliest/latest frame times.
public record FilterIntegration(string Filter, double Hours, DateTime? First, DateTime? Last);

// One scope (telescope) on a target. Hours is the sum of its filters; First/Last span them.
public record ScopeIntegration(string Scope, DateTime? First, DateTime? Last, IReadOnlyList<FilterIntegration> Filters)
{
    public double Hours => Filters.Sum(f => f.Hours);
}

// One canonical target. Hours is the sum of its scopes; First/Last span them.
public record TargetIntegration(int TargetId, string CanonicalName, DateTime? First, DateTime? Last, IReadOnlyList<ScopeIntegration> Scopes)
{
    public double Hours => Scopes.Sum(s => s.Hours);
}

// Aggregates Light-frame integration time target -> scope -> filter. Returns the tree unordered;
// ordering is a view concern.
public class TargetSummaryQuery
{
    private const string UnknownScope = "(Unknown scope)";
    private const string NoFilter = "(No filter)";
    private const string Unnamed = "(Unnamed)";

    private static readonly HashSet<string> EmptyScopeFilters = new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    public TargetSummaryQuery(IDbContextFactory<LumidexDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    private record Cell(int Id, string Name, string Scope, string Filter, double Hours, DateTime? First, DateTime? Last);

    public IReadOnlyList<TargetIntegration> GetTargetSummary()
    {
        using var db = _dbContextFactory.CreateDbContext();

        // (target, scope, filter) -> seconds + date extents, over Light frames with a mapped name.
        // Trim the ObjectName on the join so a space-padded twin matches the stored map.
        var flat = (from f in db.ImageFiles.AsNoTracking()
                    where f.Type == ImageType.Light
                    join m in db.TargetNameMaps on f.ObjectName!.Trim() equals m.RawObjectName
                    join t in db.Targets on m.TargetId equals t.Id
                    group f by new { t.Id, t.CanonicalName, f.TelescopeName, f.FilterName } into g
                    select new
                    {
                        g.Key.Id, g.Key.CanonicalName, g.Key.TelescopeName, g.Key.FilterName,
                        Seconds = g.Sum(x => (double?)x.Exposure) ?? 0.0,
                        First = g.Min(x => x.ObservationTimestampUtc),
                        Last = g.Max(x => x.ObservationTimestampUtc),
                    }).ToList();

        // Light frames with no usable ObjectName -> a synthetic "(Unnamed)" pile.
        var unnamed = (from f in db.ImageFiles.AsNoTracking()
                       where f.Type == ImageType.Light && (f.ObjectName == null || f.ObjectName!.Trim() == "")
                       group f by new { f.TelescopeName, f.FilterName } into g
                       select new
                       {
                           g.Key.TelescopeName, g.Key.FilterName,
                           Seconds = g.Sum(x => (double?)x.Exposure) ?? 0.0,
                           First = g.Min(x => x.ObservationTimestampUtc),
                           Last = g.Max(x => x.ObservationTimestampUtc),
                       }).ToList();

        // Light frames named but not yet mapped -> a self-named synthetic row (a backstop so frames
        // never silently vanish between a scan and the next resolve).
        var unmapped = (from f in db.ImageFiles.AsNoTracking()
                        where f.Type == ImageType.Light && f.ObjectName != null && f.ObjectName!.Trim() != ""
                              && !db.TargetNameMaps.Any(m => m.RawObjectName == f.ObjectName!.Trim())
                        group f by new { ObjectName = f.ObjectName!.Trim(), f.TelescopeName, f.FilterName } into g
                        select new
                        {
                            g.Key.ObjectName, g.Key.TelescopeName, g.Key.FilterName,
                            Seconds = g.Sum(x => (double?)x.Exposure) ?? 0.0,
                            First = g.Min(x => x.ObservationTimestampUtc),
                            Last = g.Max(x => x.ObservationTimestampUtc),
                        }).ToList();

        var cells = flat.Select(x => ToCell(x.Id, x.CanonicalName, x.TelescopeName, x.FilterName, x.Seconds, x.First, x.Last))
            .Concat(unnamed.Select(x => ToCell(0, Unnamed, x.TelescopeName, x.FilterName, x.Seconds, x.First, x.Last)))
            .Concat(unmapped.Select(x => ToCell(0, string.IsNullOrWhiteSpace(x.ObjectName) ? Unnamed : x.ObjectName!,
                                                x.TelescopeName, x.FilterName, x.Seconds, x.First, x.Last)))
            .ToList();

        // Canonicalize each cell's filter using the scope's whole filter set as context (the
        // bare-B/R rule), so true synonyms merge before the roll-up groups by filter.
        var scopeFilters = cells
            .GroupBy(c => c.Scope)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Filter).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var canonical = cells.Select(c => c with
        {
            Filter = FilterCanonicalizer.Canonicalize(c.Filter,
                scopeFilters.TryGetValue(c.Scope, out var sf) ? sf : EmptyScopeFilters),
        });

        return RollUp(canonical);
    }

    private static Cell ToCell(int id, string name, string? telescopeName, string? filterName, double seconds, DateTime? first, DateTime? last)
        => new(id, name,
            string.IsNullOrEmpty(telescopeName) ? UnknownScope : telescopeName,
            string.IsNullOrEmpty(filterName) ? NoFilter : filterName,
            seconds / 3600.0, first, last);

    private static IReadOnlyList<TargetIntegration> RollUp(IEnumerable<Cell> cells)
    {
        return cells
            .GroupBy(c => (c.Id, c.Name))
            .Select(tg =>
            {
                var scopes = tg.GroupBy(c => c.Scope)
                    .Select(sg =>
                    {
                        var filters = sg.GroupBy(c => c.Filter)
                            .Select(fg => new FilterIntegration(fg.Key, fg.Sum(c => c.Hours), fg.Min(c => c.First), fg.Max(c => c.Last)))
                            .ToList();
                        return new ScopeIntegration(sg.Key, filters.Min(f => f.First), filters.Max(f => f.Last), filters);
                    })
                    .ToList();
                return new TargetIntegration(tg.Key.Id, tg.Key.Name, scopes.Min(s => s.First), scopes.Max(s => s.Last), scopes);
            })
            .ToList();
    }
}
