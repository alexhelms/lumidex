namespace Lumidex.Core.Data;

public class PersistedFilter
{
    public int Id { get; set; }

    public int AppSettingsId { get; set; }

    public AppSettings AppSettings { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Data { get; set; }
}