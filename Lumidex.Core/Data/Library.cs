namespace Lumidex.Core.Data;

public class Library
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Path { get; set; } = null!;

    public DateTime? LastScan { get; set; }
}
