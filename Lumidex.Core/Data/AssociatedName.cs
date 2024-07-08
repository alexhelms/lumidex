using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

public class AssociatedName
{
    public int Id { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime? UpdatedOn { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Name { get; set; } = null!;

    public ICollection<ImageFile> Images { get; set; } = new List<ImageFile>();
}
