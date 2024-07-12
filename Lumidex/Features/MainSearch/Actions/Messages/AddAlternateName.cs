namespace Lumidex.Features.MainSearch.Actions.Messages;

public class AddAlternateName
{
    public required string Name { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}
