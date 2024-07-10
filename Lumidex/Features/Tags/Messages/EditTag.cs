using Avalonia.Media;

namespace Lumidex.Features.Tags.Messages;

public class EditTag
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public Color Color { get; init; } = Colors.Gray;
}
