using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Lumidex.Core.Targets;

// Canonical-identity maintenance for the target summary. Phase A: deterministic,
// no network. Maps each FITS-header ObjectName to a Target, folding pure FORMATTING
// variants of a name (case / whitespace / punctuation) onto one Target, and merges
// targets on user request. Cross-catalog identity (e.g. "Tarantula Nebula" == "NGC
// 2070") and the display naming convention are out of scope here — they need catalog
// resolution (Phase B).
public class TargetResolutionService
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public TargetResolutionService(IDbContextFactory<LumidexDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    // Resolve targets in two passes: first collapse any existing formatting-variant
    // duplicates (e.g. separate "NGC 2070" / "Ngc2070" targets from an earlier 1:1
    // run), then map any not-yet-mapped ObjectNames. Idempotent — a second call finds
    // nothing to merge and nothing new to add.
    public void EnsureTargetsResolved()
    {
        using var db = _dbContextFactory.CreateDbContext();
        ConsolidateFormattingVariants(db);
        MapNewObjectNames(db);
    }

    // Light identity key: lowercase, alphanumerics only. Collapses the formatting
    // variants of a name — "Bode's Galaxy"/"Bodes Galaxy", "NGC 2070"/"Ngc2070",
    // "M 101"/"M101" — to one key. It does NOT understand catalog cross-references
    // (it cannot know "Tarantula Nebula" == "NGC 2070"); names that differ by real
    // words stay distinct. Returns "" for a name with no alphanumerics.
    //
    // Knowingly lossy and not re-splittable: stripping every non-alphanumeric also folds
    // hyphenated catalog designations ("M1-67" and "M167" collapse to one key). A
    // hyphen-significance rule would re-fragment the "M 101"/"M101" formatting variants this
    // exists to merge, so it is accepted for Phase A; true catalog identity is Phase B.
    private static string NormalizeKey(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // Merge targets whose names share a NormalizeKey into the one with the most Light
    // frames (the dominant spelling), so a single object that was split across spelling
    // variants shows as one row. Reuses the same repoint-before-delete ordering as
    // MergeTargets so no name map is cascade-dropped. No-op once the library is clean.
    private static void ConsolidateFormattingVariants(LumidexDbContext db)
    {
        var keyed = db.TargetNameMaps
            .Select(m => new { m.RawObjectName, m.TargetId })
            .AsEnumerable()
            .Select(m => new { Key = NormalizeKey(m.RawObjectName), m.TargetId })
            .Where(m => m.Key.Length > 0)
            .ToList();

        var groups = keyed
            .GroupBy(m => m.Key)
            .Where(g => g.Select(m => m.TargetId).Distinct().Count() > 1)
            .ToList();
        if (groups.Count == 0)
            return;

        var frames = FrameCountsByTarget(db);
        foreach (var g in groups)
        {
            var ids = g.Select(m => m.TargetId).Distinct().ToList();
            // Survivor = most Light frames; ties fall to the smallest id for determinism.
            var survivorId = ids
                .OrderByDescending(id => frames.GetValueOrDefault(id))
                .ThenBy(id => id)
                .First();
            AbsorbInto(db, survivorId, ids.Where(id => id != survivorId).ToList());
        }

        db.SaveChanges();
    }

    // Map every distinct non-empty ObjectName to a Target. A name whose NormalizeKey
    // already belongs to a target joins it (a new map, no new target); otherwise it
    // mints a new target. Idempotent — only names not already mapped are added.
    private static void MapNewObjectNames(LumidexDbContext db)
    {
        var maps = db.TargetNameMaps
            .Select(m => new { m.RawObjectName, m.TargetId })
            .AsEnumerable()
            // Trim the stored key so the idempotency check matches trimmed names.
            .Select(m => new { Raw = m.RawObjectName.Trim(), m.TargetId })
            .ToList();

        var mappedRaw = maps.Select(m => m.Raw).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keyToTarget = maps
            // Compute the identity key once per name instead of in both the filter and
            // the group-by that follow.
            .Select(m => new { m.Raw, Key = NormalizeKey(m.Raw), m.TargetId })
            .Where(m => m.Key.Length > 0)
            .GroupBy(m => m.Key)
            // Post-consolidation there is one target per key; First() is safe.
            .ToDictionary(g => g.Key, g => g.First().TargetId);

        var names = db.ImageFiles
            .Where(f => f.ObjectName != null && f.ObjectName != "")
            .Select(f => f.ObjectName!)
            .Distinct()
            .ToList()
            // Whitespace-only names ("   ", "\t") spawn no target — symmetric with
            // MergeTargets' ThrowIfNullOrWhiteSpace. Client-side because SQLite TRIM
            // strips only spaces, not tabs/newlines.
            .Where(n => !string.IsNullOrWhiteSpace(n))
            // FITS space-pads OBJECT, so "M 31" and "M 31 " arrive distinct; trim to one
            // identity. The integration-query join trims the same way, so maps still match.
            .Select(n => n.Trim())
            .Distinct()
            .ToList();

        // Targets minted earlier in THIS loop, so a second new variant of the same key
        // joins the pending target instead of minting a duplicate before SaveChanges.
        var pendingByKey = new Dictionary<string, Target>(StringComparer.Ordinal);
        var added = false;

        foreach (var name in names)
        {
            if (!mappedRaw.Add(name))
                continue; // already mapped (case-insensitive)

            var key = NormalizeKey(name);
            if (key.Length > 0 && keyToTarget.TryGetValue(key, out var existingId))
            {
                db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = name, TargetId = existingId });
            }
            else if (key.Length > 0 && pendingByKey.TryGetValue(key, out var pending))
            {
                db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = name, Target = pending });
            }
            else
            {
                var target = new Target { CanonicalName = name };
                db.Targets.Add(target);
                db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = name, Target = target });
                if (key.Length > 0)
                    pendingByKey[key] = target;
            }
            added = true;
        }

        if (added)
            db.SaveChanges();
    }

    // Light-frame count per target, via the trimmed name-map join (mirroring the
    // integration query), so the consolidation survivor is the dominant spelling.
    private static Dictionary<int, int> FrameCountsByTarget(LumidexDbContext db)
    {
        return (from f in db.ImageFiles
                where f.Type == ImageType.Light && f.ObjectName != null && f.ObjectName != ""
                join m in db.TargetNameMaps on f.ObjectName!.Trim() equals m.RawObjectName
                group f by m.TargetId into g
                select new { TargetId = g.Key, Count = g.Count() })
               .ToDictionary(x => x.TargetId, x => x.Count);
    }

    // Merge several targets into the first id in the list, NON-DESTRUCTIVELY: record each absorbed
    // -> survivor mapping (TargetMerge). The fold is applied at query time; Undo (UndoMerge) removes
    // the records and the absorbed targets reappear intact. No-op for < 2 distinct ids. An id already
    // absorbed by an earlier merge is skipped so the read-time remap stays single-valued.
    //
    // A merge NEVER mutates a Target row — not even the survivor's name (the survivor already carries
    // the canonical name, since the UI picks the most-integrated row as survivor; canonicalName is
    // the panel's display snapshot). That keeps Undo a perfect inverse and means the goal-loss bug class
    // (per-scope goals lost on merge) cannot occur: nothing is deleted or renamed, so goal collisions
    // between the merged targets resolve via the goals dict's GroupBy+Max at read time.
    public void MergeTargets(IReadOnlyList<int> targetIds, string canonicalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);

        var ids = targetIds.Distinct().ToList();
        if (ids.Count < 2)
            return;

        using var db = _dbContextFactory.CreateDbContext();

        var survivorId = ids[0];

        // The ids come from rendered rows; the survivor could have been deleted (e.g. a concurrent
        // rescan) between render and merge. Treat a vanished survivor as a no-op.
        if (db.Targets.Find(survivorId) is null)
            return;

        var alreadyAbsorbed = db.TargetMerges.Select(m => m.AbsorbedTargetId).ToHashSet();
        var labels = db.Targets
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.CanonicalName })
            .AsEnumerable()
            .ToDictionary(t => t.Id, t => t.CanonicalName);

        var operationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var newRecords = ids.Where(id => id != survivorId && !alreadyAbsorbed.Contains(id))
            .Select(absorbedId => new TargetMerge
            {
                OperationId = operationId,
                SurvivorTargetId = survivorId,
                AbsorbedTargetId = absorbedId,
                SurvivorLabel = canonicalName,
                AbsorbedLabel = labels.GetValueOrDefault(absorbedId, $"Target {absorbedId}"),
                CreatedUtc = now,
            })
            .ToList();
        if (newRecords.Count == 0)
            return;

        db.TargetMerges.AddRange(newRecords);
        db.SaveChanges();
    }

    // Fold several telescope-name spellings into one canonical scope, NON-DESTRUCTIVELY: record each
    // absorbed -> canonical mapping (ScopeMerge). Applied at query time; Undo removes the records. A
    // blank name, the canonical itself, an in-call duplicate, or a name already absorbed by an
    // earlier merge is skipped so the read-time remap stays single-valued.
    public void MergeScopes(string canonicalName, IReadOnlyList<string> absorbedNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);
        var canonical = canonicalName.Trim();   // store the canonical trimmed, like the absorbed names

        using var db = _dbContextFactory.CreateDbContext();

        var alreadyAbsorbed = db.ScopeMerges.Select(m => m.AbsorbedName)
            .AsEnumerable()
            .Select(n => n.ToLowerInvariant())
            .ToHashSet();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { canonical };

        var operationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var newRecords = new List<ScopeMerge>();
        foreach (var name in absorbedNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var trimmed = name.Trim();
            if (!seen.Add(trimmed))                       // canonical itself or a repeat in this call
                continue;
            if (alreadyAbsorbed.Contains(trimmed.ToLowerInvariant()))
                continue;                                  // already folded by an earlier merge
            newRecords.Add(new ScopeMerge
            {
                OperationId = operationId,
                CanonicalName = canonical,
                AbsorbedName = trimmed,
                CreatedUtc = now,
            });
        }
        if (newRecords.Count == 0)
            return;

        db.ScopeMerges.AddRange(newRecords);
        db.SaveChanges();
    }

    // Reverse one merge action (scope or target) by removing every record that shares its
    // OperationId. The absorbed scopes/targets reappear on the next query — nothing to restore,
    // since the merge never deleted anything.
    public void UndoMerge(Guid operationId)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.ScopeMerges.RemoveRange(db.ScopeMerges.Where(m => m.OperationId == operationId));
        db.TargetMerges.RemoveRange(db.TargetMerges.Where(m => m.OperationId == operationId));
        db.SaveChanges();
    }

    // Every active user merge, newest first, for the Manage-merges panel. Scope and target merges
    // unify into one MergeOperation list, each grouped by OperationId (a 3-way merge is one
    // operation carrying two absorbed labels).
    public IReadOnlyList<MergeOperation> GetMergeOperations()
    {
        using var db = _dbContextFactory.CreateDbContext();

        // The survivor's live name: auto-consolidation can rename a target after a merge, so the
        // panel reads the current CanonicalName (falling back to the snapshot label if the survivor
        // is gone) — otherwise the snapshot would show a name that no longer matches the folded row.
        // All rows in one OperationId share their survivor, so First() picks the operation's label.
        var targetNames = db.Targets.AsNoTracking()
            .Select(t => new { t.Id, t.CanonicalName })
            .AsEnumerable()
            .ToDictionary(t => t.Id, t => t.CanonicalName);

        var scopeOps = db.ScopeMerges.AsNoTracking().ToList()
            .GroupBy(m => m.OperationId)
            .Select(g => new MergeOperation(g.Key, MergeKind.Scope,
                g.First().CanonicalName,   // scope names are FITS strings, never renamed — snapshot is live
                g.Select(m => m.AbsorbedName).ToList(),
                g.Max(m => m.CreatedUtc)));

        var targetOps = db.TargetMerges.AsNoTracking().ToList()
            .GroupBy(m => m.OperationId)
            .Select(g => new MergeOperation(g.Key, MergeKind.Target,
                targetNames.TryGetValue(g.First().SurvivorTargetId, out var live) ? live : g.First().SurvivorLabel,
                g.Select(m => m.AbsorbedLabel).ToList(),
                g.Max(m => m.CreatedUtc)));

        return scopeOps.Concat(targetOps).OrderByDescending(o => o.CreatedUtc).ToList();
    }

    // Fold the absorbed targets into the survivor: repoint their name maps and per-filter
    // goals onto it, then delete them — staged on the context so the caller's single
    // SaveChanges commits the repoint and the delete atomically. EF's Immediate cascade-timing
    // evaluates the cascade against the tracked FK (already the survivor), so nothing is
    // cascade-dropped. Used ONLY by ConsolidateFormattingVariants now — the auto-fold of
    // NormalizeKey-identical spellings, which stays destructive (reversing an auto-fold is pointless,
    // it would re-fold next reload). User merges went non-destructive (MergeTargets / MergeScopes
    // record-and-resolve), so this goal-protecting repoint-before-delete guards only the auto path.
    //
    // Goals need more than a blind repoint. TargetFilterGoal's Scope/Filter columns are BINARY
    // but their sources (TelescopeName / FilterName) are NOCASE, so "T20"/"t20" (or "Ha"/"ha")
    // are one physical scope+filter yet two distinct (TargetId, Scope, Filter) unique-index
    // keys. We collapse each (scope, filter) case-insensitively and keep exactly ONE row on the
    // survivor — the larger goal wins; every other row in the group is removed in this same
    // SaveChanges. Repointing two rows onto one key, or leaving two case-variant rows, would
    // either abort the merge on the unique index or strand a duplicate goal.
    private static void AbsorbInto(LumidexDbContext db, int survivorId, IReadOnlyList<int> absorbedIds)
    {
        if (absorbedIds.Count == 0)
            return;

        // Name maps carry no unique constraint on the scope side, so a plain repoint is safe.
        foreach (var map in db.TargetNameMaps.Where(m => absorbedIds.Contains(m.TargetId)))
            map.TargetId = survivorId;

        // Per-(scope, filter) goals: collision-aware merge over the survivor's + absorbed rows.
        // Fold scope+filter with ToLowerInvariant — the SAME keys TargetGoalQuery's lookup uses,
        // so the merge and the read agree on what "the same (scope, filter)" is.
        var allIds = absorbedIds.Append(survivorId).ToList();
        var goals = db.TargetFilterGoals.Where(g => allIds.Contains(g.TargetId)).ToList();
        foreach (var key in goals.GroupBy(g => (g.Scope.ToLowerInvariant(), g.Filter.ToLowerInvariant())))
        {
            var winningGoal = key.Max(g => g.GoalHours);
            // Prefer the survivor's own row as the keeper (no repoint needed); otherwise repoint
            // exactly one absorbed row. Either way only ONE row ends up on the survivor for this
            // (scope, filter), so the unique index never sees a duplicate.
            var keeper = key.FirstOrDefault(g => g.TargetId == survivorId) ?? key.First();
            keeper.TargetId = survivorId;
            keeper.GoalHours = winningGoal;
            foreach (var loser in key.Where(g => !ReferenceEquals(g, keeper)))
                db.TargetFilterGoals.Remove(loser);
        }

        // A user TargetMerge points at target ids. If this auto-fold deletes a target a user merge
        // kept as its SURVIVOR, that merge would silently un-merge (the dangling survivor is skipped
        // at read time) and leave a ghost row in the Manage-merges panel. Repoint those records onto
        // the new survivor so the user's merge follows the consolidation; drop any that would now
        // self-reference (absorbed == survivor). AbsorbedTargetId is untouched, so the unique index
        // on it is never violated. Staged on the same context -> committed in the caller's SaveChanges.
        foreach (var m in db.TargetMerges.Where(m => absorbedIds.Contains(m.SurvivorTargetId)).ToList())
        {
            if (m.AbsorbedTargetId == survivorId)
                db.TargetMerges.Remove(m);
            else
                m.SurvivorTargetId = survivorId;
        }

        db.Targets.RemoveRange(db.Targets.Where(t => absorbedIds.Contains(t.Id)));
    }
}
