using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

// One absorbed-telescope-name -> canonical-name mapping from a user "Merge Scopes" action.
// NON-DESTRUCTIVE: the frames keep their raw FITS TelescopeName; the fold is applied at query time
// (TargetGoalQuery resolves scope names through these rows before the roll-up), so deleting the row
// (Undo) re-separates the scopes with zero data loss. OperationId groups the rows of one merge
// (folding three scopes into one writes two rows) so Undo reverses the whole action atomically.
//
// Names are NOCASE to match the FITS TelescopeName collation (the library logs "T20"/"t20"); the
// resolver also lowercases its keys, so the read path and the de-dup guard agree on identity.
//
// AbsorbedName is UNIQUE — one survivor per absorbed name — the single-valued-map invariant the
// resolver assumes. The application-level de-dup guard in MergeScopes enforces it within one process;
// the unique index is the database backstop that catches a cross-instance race (it inherits the
// column's NOCASE collation, so "T20"/"t20" count as the same absorbed name, matching the resolver).
[Index(nameof(AbsorbedName), IsUnique = true)]
public class ScopeMerge
{
    [Key]
    public int Id { get; set; }

    public Guid OperationId { get; set; }

    // The kept spelling (survivor). New scope rows and goals roll up under this name.
    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string CanonicalName { get; set; } = null!;

    // The folded spelling. At most one ScopeMerge per AbsorbedName (enforced in MergeScopes) keeps
    // the absorbed -> canonical remap single-valued.
    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string AbsorbedName { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
}
