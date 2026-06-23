using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Core.Data;

// One absorbed-target -> survivor-target mapping from a user "Merge Targets" action. NON-DESTRUCTIVE:
// the absorbed Target row and its maps/goals are left intact; the fold is applied at query time, so
// Undo (delete the row) restores it whole. OperationId groups one merge's rows for atomic undo. It is
// non-destructive precisely to avoid the per-scope-goal cascade-loss a destructive merge would cause.
//
// DELIBERATELY NO foreign key to Target. ConsolidateFormattingVariants can delete a Target on any
// reload; a Restrict FK would throw and a Cascade FK would silently un-merge. Plain ints + defensive
// resolution (a dangling survivor id is skipped) decouple the record from the Target lifecycle.
// Labels snapshot the names at merge time (the panel resolves the survivor's live name on top).
//
// AbsorbedTargetId is UNIQUE — one survivor per absorbed target — the single-valued-map invariant the
// resolver assumes; the index is the DB backstop for the cross-instance race the in-process de-dup
// guard can't cover.
[Index(nameof(AbsorbedTargetId), IsUnique = true)]
public class TargetMerge
{
    [Key]
    public int Id { get; set; }

    public Guid OperationId { get; set; }

    public int SurvivorTargetId { get; set; }
    public int AbsorbedTargetId { get; set; }

    public string SurvivorLabel { get; set; } = null!;
    public string AbsorbedLabel { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
