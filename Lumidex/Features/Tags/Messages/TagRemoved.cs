namespace Lumidex.Features.Tags.Messages;

public class TagRemoved
{
    public required TagViewModel Tag { get; init; }
    public required IList<ImageFileViewModel> ImageFiles { get; init; }
}
