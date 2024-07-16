using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

[Index(nameof(ObjectName))]
[Index(nameof(Alias))]
public class ObjectAlias
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string ObjectName { get; set; } = null!;

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Alias { get; set; } = null!;
}