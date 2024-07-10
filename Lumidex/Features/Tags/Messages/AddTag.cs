namespace Lumidex.Features.Tags.Messages;

public class AddTag
{
    public required TagViewModel Tag { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}
