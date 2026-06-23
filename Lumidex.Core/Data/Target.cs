using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

public class Target
{
    public int Id { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string CanonicalName { get; set; } = null!;

    // Reserved for future SIMBAD / coordinate resolution; added nullable now so that
    // work won't need a second migration. Unused so far.
    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? SimbadId { get; set; }
    public double? Ra { get; set; }
    public double? Dec { get; set; }

    public ICollection<TargetNameMap> NameMaps { get; set; } = new List<TargetNameMap>();
}
