namespace Lumidex.Features.Aliases.Messages;

public class CreateAlias
{
    public required string ObjectName { get; init; }
    public required string Alias { get; init; }
}
