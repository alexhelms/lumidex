using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

public class AstrobinFilter
{
    public int Id { get; set; }
    
    public int AstrobinId { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Name { get; set; } = string.Empty;
}
