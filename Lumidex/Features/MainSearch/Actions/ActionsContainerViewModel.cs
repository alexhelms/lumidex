namespace Lumidex.Features.MainSearch.Actions;

public partial class ActionsContainerViewModel : ViewModelBase
{
    [ObservableProperty] ObservableCollectionEx<ActionViewModelBase> _items = new();
    [ObservableProperty] ActionViewModelBase? _selectedItem;

    public ActionsContainerViewModel(
        TagsActionViewModel tagsViewModel)
    {
        _items.AddRange([
            tagsViewModel,
        ]);

        // HACK: activate each item so the messenger can be registered
        foreach (var item in Items)
        {
            item.IsActive = true;
            item.IsActive = false;
        }

        SelectedItem = tagsViewModel;
    }
}
