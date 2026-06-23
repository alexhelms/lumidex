using Microsoft.EntityFrameworkCore;

namespace Lumidex.Core.Data;

// A per-(telescope, filter) integration goal in hours — the granularity goals are ENTERED at
// in the Target Summary v2. Scope and Target goals are derived sums of their filter goals and
// are never stored. (TargetId, Scope, Filter) is unique: one goal per filter per telescope per
// target. Scope/Filter are the same strings the integration query buckets on — the FITS
// TelescopeName and the canonical FilterName, or the "(Unknown scope)" / "(No filter)"
// fallbacks.
//
// The FK to Target cascade-deletes by convention; the merge paths repoint goals onto the
// survivor before deleting an absorbed target (TargetResolutionService.AbsorbInto) so a merge
// does not cascade-drop them — keyed on (Scope, Filter) and de-duped case-insensitively, since
// these columns are BINARY but their sources (TelescopeName / FilterName) are NOCASE.
//
// Scope and Filter are raw mutable strings, not ids: renaming a telescope or filter strands the
// goal row under the old name (visible nowhere, recoverable only by editing the DB). A re-key
// path is Phase B, not warranted while the feature carries no shipped data.
[Index(nameof(TargetId), nameof(Scope), nameof(Filter), IsUnique = true)]
public class TargetFilterGoal
{
    public int Id { get; set; }
    public int TargetId { get; set; }
    public Target Target { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Filter { get; set; } = null!;
    public double GoalHours { get; set; }
}
