namespace Lumidex.Features.Library.Messages;

public class CreateLibrary
{
    public required string Name { get; init; }
    public required string Path { get; init; }
}
