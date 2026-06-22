using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Lumidex.Core.Targets;

// Maps each FITS-header OBJECT string to a canonical Target so the summary can aggregate per target.
// Deterministic and offline: it folds pure FORMATTING variants of a name (case / whitespace /
// punctuation) onto one target. Cross-catalog identity ("Tarantula Nebula" == "NGC 2070") is out of
// scope here.
public class TargetResolutionService
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public TargetResolutionService(IDbContextFactory<LumidexDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    // Two passes: first collapse any existing formatting-variant duplicates, then map any not-yet-
    // mapped names. Idempotent — a second call finds nothing to merge and nothing new to add.
    public void EnsureTargetsResolved()
    {
        using var db = _dbContextFactory.CreateDbContext();
        ConsolidateFormattingVariants(db);
        MapNewObjectNames(db);
    }

    // Light identity key: lowercase, alphanumerics only. Collapses formatting variants of a name —
    // "Bode's Galaxy"/"Bodes Galaxy", "NGC 2070"/"Ngc2070", "M 101"/"M101" — to one key. It does not
    // understand catalog cross-references; names differing by real words stay distinct. Returns "" for
    // a name with no alphanumerics.
    //
    // Knowingly lossy: stripping every non-alphanumeric also folds hyphenated designations ("M1-67"
    // and "M167" collapse to one key). A hyphen-significance rule would re-split the "M 101"/"M101"
    // variants this exists to merge, so it is accepted here; true catalog identity is future work.
    private static string NormalizeKey(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    // Merge targets whose names share a NormalizeKey into the one with the most Light frames (the
    // dominant spelling), so an object split across spelling variants shows as one row. No-op once the
    // library is clean.
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

    // Map every distinct object name to a target. A name whose NormalizeKey already belongs to a
    // target joins it (a new map, no new target); otherwise it mints a target. Idempotent — only
    // names not already mapped are added.
    private static void MapNewObjectNames(LumidexDbContext db)
    {
        var maps = db.TargetNameMaps
            .Select(m => new { m.RawObjectName, m.TargetId })
            .AsEnumerable()
            .Select(m => new { Raw = m.RawObjectName.Trim(), m.TargetId })
            .ToList();

        var mappedRaw = maps.Select(m => m.Raw).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keyToTarget = maps
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
            // Whitespace-only names spawn no target; trim the rest. Client-side because SQLite TRIM
            // strips only spaces.
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct()
            .ToList();

        // Targets minted earlier in THIS loop, so a second new variant of the same key joins the
        // pending target instead of minting a duplicate before SaveChanges.
        var pendingByKey = new Dictionary<string, Target>(StringComparer.Ordinal);
        var added = false;

        foreach (var name in names)
        {
            if (!mappedRaw.Add(name))
                continue;

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

    // Light-frame count per target, via the trimmed name-map join, so consolidation keeps the
    // dominant spelling.
    private static Dictionary<int, int> FrameCountsByTarget(LumidexDbContext db)
    {
        return (from f in db.ImageFiles
                where f.Type == ImageType.Light && f.ObjectName != null && f.ObjectName != ""
                join m in db.TargetNameMaps on f.ObjectName!.Trim() equals m.RawObjectName
                group f by m.TargetId into g
                select new { TargetId = g.Key, Count = g.Count() })
               .ToDictionary(x => x.TargetId, x => x.Count);
    }

    // Fold the absorbed targets into the survivor: repoint their name maps onto it, then delete them.
    // Staged on the context so the caller's single SaveChanges commits the repoint and delete
    // together.
    private static void AbsorbInto(LumidexDbContext db, int survivorId, IReadOnlyList<int> absorbedIds)
    {
        if (absorbedIds.Count == 0)
            return;

        foreach (var map in db.TargetNameMaps.Where(m => absorbedIds.Contains(m.TargetId)))
            map.TargetId = survivorId;

        db.Targets.RemoveRange(db.Targets.Where(t => absorbedIds.Contains(t.Id)));
    }
}
