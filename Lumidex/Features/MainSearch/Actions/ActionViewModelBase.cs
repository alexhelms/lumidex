using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch.Actions;

public abstract partial class ActionViewModelBase : ValidatableViewModelBase,
    IRecipient<SelectedSearchResultsChanged>
{
    protected HashSet<int> SelectedIds { get; private set; } = new();

    [ObservableProperty] string _displayName = typeof(ActionViewModelBase).Name;
    [ObservableProperty] AvaloniaList<ImageFileViewModel> _selectedItems = new();

    public void Receive(SelectedSearchResultsChanged message)
    {
        SelectedItems = message.Items;
        SelectedIds = new(message.Items.Select(f => f.Id));
        OnSelectedItemsChanged();
    }

    protected virtual void OnSelectedItemsChanged() { }
}
