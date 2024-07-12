namespace Lumidex.Features.MainSearch.Actions.Messages;

public class AlternateNameAdded
{
    public required AlternateNameViewModel AlternateName { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}