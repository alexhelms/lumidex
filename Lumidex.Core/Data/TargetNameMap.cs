using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

// One raw FITS-header OBJECT string maps to exactly one canonical Target. RawObjectName is unique
// (NOCASE) and resolution trims it before keying, so ASCII-case variants ("M 31"/"m 31") and
// whitespace-padded twins ("M 31 ") collapse to one map. The fold is ASCII-case + whitespace-trim
// only; non-ASCII case variants are not folded, which is fine for the Latin/numeric catalog
// designations FITS OBJECT carries in practice.
[Index(nameof(RawObjectName), IsUnique = true)]
public class TargetNameMap
{
    public int Id { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string RawObjectName { get; set; } = null!;

    public int TargetId { get; set; }
    public Target Target { get; set; } = null!;
}
