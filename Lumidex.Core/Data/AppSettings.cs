using System.ComponentModel.DataAnnotations;

namespace Lumidex.Core.Data;

public class AppSettings
{
    public int Id { get; set; }

    public ICollection<AstrobinFilter> AstrobinFilters { get; set; } = new List<AstrobinFilter>();
}
