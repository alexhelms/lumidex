using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

[Index(nameof(Name), IsUnique = true)]
public class AlternateName
{
    public int Id { get; set; }

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime? UpdatedOn { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE UNIQUE")]
    public string Name { get; set; } = null!;
}
