namespace Lumidex.Core.Data;

public class EquipmentTag
{
    public int Id { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long Color { get; set; }
}
