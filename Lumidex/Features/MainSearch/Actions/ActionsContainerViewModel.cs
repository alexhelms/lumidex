namespace Lumidex.Features.MainSearch.Actions;

public partial class ActionsContainerViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ObservableCollectionEx<ActionViewModelBase> Items { get; set; } = new();

    [ObservableProperty]
    public partial ActionViewModelBase? SelectedItem { get; set; }

    public ActionsContainerViewModel(
        ImageFileInfoViewModel infoViewModel,
        TagsActionViewModel tagsViewModel)
    {
        Items.AddRange([
            infoViewModel,
            tagsViewModel,
        ]);

        // HACK: activate each item so the messenger can be registered
        foreach (var item in Items)
        {
            item.IsActive = true;
            item.IsActive = false;
        }

        SelectedItem = infoViewModel;
    }
}
