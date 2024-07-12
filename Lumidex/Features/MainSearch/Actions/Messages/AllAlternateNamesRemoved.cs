namespace Lumidex.Features.MainSearch.Actions.Messages;

public class AllAlternateNamesRemoved
{
    public required IEnumerable<AlternateNameViewModel> AlternateNames { get; init; } = [];
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}
