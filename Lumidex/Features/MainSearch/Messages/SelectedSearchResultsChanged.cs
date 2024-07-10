namespace Lumidex.Features.MainSearch.Messages;

public class SelectedSearchResultsChanged
{
    public AvaloniaList<ImageFileViewModel> Items { get; }

    public SelectedSearchResultsChanged(AvaloniaList<ImageFileViewModel> items)
    {
        Items = items;
    }
}
