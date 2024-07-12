namespace Lumidex.Features.MainSearch.Actions.Messages;

public class RemoveAllAlternateNames
{
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}
