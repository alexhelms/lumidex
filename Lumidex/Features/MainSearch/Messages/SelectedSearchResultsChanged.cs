namespace Lumidex.Features.MainSearch.Messages;

public class SelectedSearchResultsChanged
{
    public ObservableCollectionEx<ImageFileViewModel> Items { get; }

    public SelectedSearchResultsChanged(ObservableCollectionEx<ImageFileViewModel> items)
    {
        Items = items;
    }
}
