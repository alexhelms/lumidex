namespace Lumidex.Features.MainSearch.Messages;

public class SearchResultsReady
{
    public ObservableCollectionEx<ImageFileViewModel> SearchResults { get; init; } = new();
}
