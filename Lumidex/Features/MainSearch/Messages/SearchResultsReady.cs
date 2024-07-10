namespace Lumidex.Features.MainSearch.Messages;

public class SearchResultsReady
{
    public AvaloniaList<ImageFileViewModel> SearchResults { get; init; } = new();
}
