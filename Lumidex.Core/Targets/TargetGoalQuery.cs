using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Core.Targets;

// One filter's integration on one scope. Hours = acquired; ExplicitGoal = the stored goal or
// null when unset; First/Last = the earliest/latest frame's observation time (for date sorts).
public record FilterGoal(string Filter, double Hours, double? ExplicitGoal, DateTime? First, DateTime? Last)
{
    // Goals v2 enters goals only on filters; an UNSET filter's goal defaults to its current
    // acquired hours, so it reads 100% and contributes its actual to the rolled-up goal.
    public double EffectiveGoal => ExplicitGoal ?? Hours;
}

// One scope (telescope) on a target. Hours and Goal are derived sums of its filters, so they
// stay consistent by construction; First/Last span its filters.
public record ScopeGoal(string Scope, DateTime? First, DateTime? Last, IReadOnlyList<FilterGoal> Filters)
{
    public double Hours => Filters.Sum(f => f.Hours);
    public double Goal => Filters.Sum(f => f.EffectiveGoal);
}

// One canonical target. Hours and Goal are derived sums of its scopes; First/Last span them.
public record TargetGoal(int TargetId, string CanonicalName, DateTime? First, DateTime? Last, IReadOnlyList<ScopeGoal> Scopes)
{
    public double Hours => Scopes.Sum(s => s.Hours);
    public double Goal => Scopes.Sum(s => s.Goal);
}

// Aggregates Light-frame integration target -> scope -> filter, attaches the per-filter goal
// (from TargetFilterGoals), and rolls scope/target goals up as derived sums. Returns the tree
// UNORDERED — sorting is a per-layer view concern (the summary's sort control); filter rows get
// the FilterClassifier's static order in the view.
public class TargetGoalQuery
{
    private const string UnknownScope = "(Unknown scope)";
    private const string NoFilter = "(No filter)";
    private const string Unnamed = "(Unnamed)";

    private static readonly HashSet<string> EmptyScopeFilters = new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    public TargetGoalQuery(IDbContextFactory<LumidexDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    private record Cell(int Id, string Name, string Scope, string Filter, double Hours, DateTime? First, DateTime? Last);

    public IReadOnlyList<TargetGoal> GetTargetGoals()
    {
        using var db = _dbContextFactory.CreateDbContext();

        // Active user merges (non-destructive): absorbed -> survivor lookups applied in memory
        // before the roll-up, so a merged scope/target folds into its survivor with no physical
        // delete (Undo just removes the record). See MergeResolver.
        // The merge tables are read whole each reload — one row per absorbed name/id (tens at most),
        // so a full read beats any lookup index; their only index is the UNIQUE integrity constraint.
        var scopeMap = MergeResolver.BuildScopeMap(db.ScopeMerges.AsNoTracking().ToList());
        var targetMap = MergeResolver.BuildTargetMap(db.TargetMerges.AsNoTracking().ToList());
        var targetNames = db.Targets.AsNoTracking()
            .Select(t => new { t.Id, t.CanonicalName })
            .AsEnumerable()
            .ToDictionary(t => t.Id, t => t.CanonicalName);
        // A scope fold maps any cell's scope; a target fold maps only real targets (id 0 is the
        // synthetic "(Unnamed)" pile) and a survivor deleted by auto-consolidation is skipped.
        string ResolveScope(string scope) =>
            scopeMap.TryGetValue(scope.ToLowerInvariant(), out var canon) ? canon : scope;
        int ResolveTarget(int id) =>
            id != 0 && targetMap.TryGetValue(id, out var sid) && targetNames.ContainsKey(sid) ? sid : id;

        // (target, scope, filter) -> seconds + date extents, over Light frames with a mapped
        // name. Trim the ObjectName on the join so a space-padded twin matches the stored map.
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

        // Light frames with no usable ObjectName -> synthetic "(Unnamed)".
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

        // Light frames named but unmapped -> a self-named synthetic row, so their hours still count.
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
            // A whitespace-only unmapped name folds to "(Unnamed)" (the SQL predicate can't catch tabs).
            .Concat(unmapped.Select(x => ToCell(0, string.IsNullOrWhiteSpace(x.ObjectName) ? Unnamed : x.ObjectName!,
                                                x.TelescopeName, x.FilterName, x.Seconds, x.First, x.Last)))
            .ToList();

        // Fold absorbed scope names + target ids onto their survivors before anything groups on
        // them. A remapped cell takes the survivor's canonical name (guaranteed present, since
        // ResolveTarget only returns an id that exists in targetNames).
        cells = cells.Select(c =>
        {
            var id = ResolveTarget(c.Id);
            return c with { Id = id, Name = id != c.Id ? targetNames[id] : c.Name, Scope = ResolveScope(c.Scope) };
        }).ToList();

        // Canonicalize each cell's filter (per-scope context for the bare-B/R rule), so true
        // synonyms merge before the roll-up groups by filter.
        var scopeFilters = cells
            .GroupBy(c => c.Scope)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Filter).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var canonical = cells.Select(c => c with
        {
            Filter = FilterCanonicalizer.Canonicalize(c.Filter,
                scopeFilters.TryGetValue(c.Scope, out var sf) ? sf : EmptyScopeFilters),
        });

        // Explicit per-(target, scope, filter) goals, keyed case-INSENSITIVELY (BINARY columns,
        // NOCASE sources — see AbsorbInto for the full collation rationale).
        // A goal stored under an absorbed target/scope must key under the survivor so it rolls up on
        // the merged row; apply the SAME ResolveScope/ResolveTarget the cell fold above uses. KEEP
        // THE TWO IN SYNC — if a future change remaps cells differently, remap goals the same way or
        // goals land on the wrong row (GetTargetGoals_ScopeMerge_GoalUnderAbsorbedScope... guards it).
        // GroupBy+Max picks the larger when both merged sides set a goal for the same filter.
        var goals = db.TargetFilterGoals.AsNoTracking()
            .Select(g => new { g.TargetId, g.Scope, g.Filter, g.GoalHours })
            .AsEnumerable()
            .GroupBy(g => (TargetId: ResolveTarget(g.TargetId), Scope: ResolveScope(g.Scope).ToLowerInvariant(), Filter: g.Filter.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Max(x => x.GoalHours));

        return RollUp(canonical, goals);
    }

    private static Cell ToCell(int id, string name, string? telescopeName, string? filterName, double seconds, DateTime? first, DateTime? last)
        => new(id, name,
            string.IsNullOrEmpty(telescopeName) ? UnknownScope : telescopeName,
            string.IsNullOrEmpty(filterName) ? NoFilter : filterName,
            seconds / 3600.0, first, last);

    private static IReadOnlyList<TargetGoal> RollUp(
        IEnumerable<Cell> cells,
        IReadOnlyDictionary<(int TargetId, string Scope, string Filter), double> goals)
    {
        return cells
            .GroupBy(c => (c.Id, c.Name))
            .Select(tg =>
            {
                var scopes = tg.GroupBy(c => c.Scope)
                    .Select(sg =>
                    {
                        var scopeKey = sg.Key.ToLowerInvariant();
                        // The scope's rendered (canonical) filter labels — the guard for the
                        // bare-B/R flip fallback in LookupGoal below.
                        var cellFilters = sg.Select(c => c.Filter).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var filters = sg.GroupBy(c => c.Filter)
                            .Select(fg => new FilterGoal(
                                fg.Key,
                                fg.Sum(c => c.Hours),
                                LookupGoal(goals, tg.Key.Id, scopeKey, fg.Key, cellFilters),
                                fg.Min(c => c.First),   // Min over DateTime? ignores nulls, null when all null
                                fg.Max(c => c.Last)))
                            .ToList();
                        return new ScopeGoal(sg.Key, filters.Min(f => f.First), filters.Max(f => f.Last), filters);
                    })
                    .ToList();
                return new TargetGoal(tg.Key.Id, tg.Key.Name, scopes.Min(s => s.First), scopes.Max(s => s.Last), scopes);
            })
            .ToList();
    }

    // Case-insensitive goal lookup: exact key first, then the bare-B/R flip partner. A goal is
    // stored under the CANONICAL label at save time, but bare B/R canonicalize from the scope's
    // whole filter set — so a photometric run landing (or a scope merge) relabels the cell
    // ("Blue" -> "B") and the stored goal would silently stop matching. The partner lookup keeps
    // it attached. Guard: only borrow when the partner label has no cell of its own in this
    // target's scope group (goal keys include the TargetId, so only same-target cells could
    // ever double-attach) — when a scope legitimately shows BOTH (raw word "Blue" beside a
    // photometric bare "B"), the partner-labelled goal belongs to that other cell, and
    // borrowing it would show and roll up the same goal twice.
    private static double? LookupGoal(
        IReadOnlyDictionary<(int TargetId, string Scope, string Filter), double> goals,
        int targetId, string scopeKey, string filter, IReadOnlySet<string> scopeCellFilters)
    {
        if (goals.TryGetValue((targetId, scopeKey, filter.ToLowerInvariant()), out var goal))
            return goal;
        return FilterCanonicalizer.FlipPartnerOf(filter) is { } partner
            && !scopeCellFilters.Contains(partner)
            && goals.TryGetValue((targetId, scopeKey, partner.ToLowerInvariant()), out var flipped)
            ? flipped : null;
    }
}
