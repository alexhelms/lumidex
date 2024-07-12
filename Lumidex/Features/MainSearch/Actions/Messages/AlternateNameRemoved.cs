namespace Lumidex.Features.MainSearch.Actions.Messages;

public class AlternateNameRemoved
{
    public required AlternateNameViewModel AlternateName { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}