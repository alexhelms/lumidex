using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Core.Targets;

// Maps each FITS-header OBJECT string to a canonical Target so the summary can aggregate per
// target. Deterministic and offline: a distinct object name mints a target the first time it is
// seen and reuses it thereafter.
public class TargetResolutionService
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public TargetResolutionService(IDbContextFactory<LumidexDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    // Ensure every Light-frame object name has a target. Idempotent — a second call adds nothing.
    public void EnsureTargetsResolved()
    {
        using var db = _dbContextFactory.CreateDbContext();
        MapNewObjectNames(db);
    }

    // Mint a target + name map for each distinct object name not already mapped. Names are matched
    // case-insensitively (the map is unique NOCASE) and trimmed, since FITS space-pads OBJECT.
    private static void MapNewObjectNames(LumidexDbContext db)
    {
        var mapped = db.TargetNameMaps
            .Select(m => m.RawObjectName)
            .AsEnumerable()
            .Select(n => n.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var names = db.ImageFiles
            .Where(f => f.ObjectName != null && f.ObjectName != "")
            .Select(f => f.ObjectName!)
            .Distinct()
            .ToList()
            // Whitespace-only names spawn no target; trim the rest to one identity. Client-side
            // because SQLite TRIM strips only spaces, not tabs/newlines.
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct()
            .ToList();

        var added = false;
        foreach (var name in names)
        {
            if (!mapped.Add(name))
                continue;

            var target = new Target { CanonicalName = name };
            db.Targets.Add(target);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = name, Target = target });
            added = true;
        }

        if (added)
            db.SaveChanges();
    }
}
