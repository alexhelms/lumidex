using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

// A canonical imaging target. One or more raw FITS OBJECT strings map to it through
// TargetNameMap; the target summary aggregates integration time per target.
public class Target
{
    public int Id { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string CanonicalName { get; set; } = null!;

    public ICollection<TargetNameMap> NameMaps { get; set; } = new List<TargetNameMap>();
}
