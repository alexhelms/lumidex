using System.ComponentModel;

namespace Lumidex.Core.Data;

public class Tag
{
    public int Id { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public string Name { get; set; } = null!;

    public string Color { get; set; } = "#ffffffff";

    public ICollection<ImageFile> TaggedImages { get;set; } = new List<ImageFile>();
}
