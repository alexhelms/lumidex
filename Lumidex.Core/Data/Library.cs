using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

public class Library
{
    public int Id { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime? UpdatedOn { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Name { get; set; } = null!;

    public string Path { get; set; } = null!;

    [Column(TypeName = "DATETIME")]
    public DateTime? LastScan { get; set; }
}
