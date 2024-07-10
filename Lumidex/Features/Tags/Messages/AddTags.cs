namespace Lumidex.Features.Tags.Messages;

public class AddTags
{
    public required IEnumerable<TagViewModel> Tags { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; }
}
