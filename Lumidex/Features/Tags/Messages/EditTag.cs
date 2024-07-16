using Avalonia.Media;

namespace Lumidex.Features.Tags.Messages;

public class EditTag
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Color { get; init; } = "#ff808080";
}
