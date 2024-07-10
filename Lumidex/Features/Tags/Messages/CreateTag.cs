using Avalonia.Media;

namespace Lumidex.Features.Tags.Messages;

public class CreateTag
{
    public required string Name { get; init; }
    public Color Color { get; init; } = Colors.Gray;
}
