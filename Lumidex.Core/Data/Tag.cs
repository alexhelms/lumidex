using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

public class Tag
{
    public const string DefaultColor = "#ff808080";

    public int Id { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime? UpdatedOn { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Name { get; set; } = null!;

    public string Color { get; set; } = "#ffffffff";
}
