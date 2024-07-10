namespace Lumidex.Features.Tags.Messages;

public class TagsCleared
{
    public required IList<ImageFileViewModel> ImageFiles { get; init; }
}
