using Avalonia.Threading;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch.Actions;

public abstract partial class ActionViewModelBase : ValidatableViewModelBase,
    IRecipient<SelectedSearchResultsChanged>,
    IRecipient<ImageFilesRemoved>
{
    protected HashSet<int> SelectedIds { get; private set; } = new();

    [ObservableProperty] string _displayName = typeof(ActionViewModelBase).Name;
    [ObservableProperty] ObservableCollectionEx<ImageFileViewModel> _selectedItems = new();

    public void Receive(SelectedSearchResultsChanged message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            SelectedItems = message.Items;
            SelectedIds = new(message.Items.Select(f => f.Id));
            OnSelectedItemsChanged();
        });
    }

    public void Receive(ImageFilesRemoved message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var imageFile in message.ImageFiles)
            {
                SelectedItems.Remove(imageFile);
                SelectedIds.Remove(imageFile.Id);
            }

            OnSelectedItemsChanged();
        });
    }

    protected virtual void OnSelectedItemsChanged() { }
}
