using Lumidex.Messages;

namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarViewModel : ViewModelBase
{
    public const string SearchTabName = "Search";
    public const string LibraryTabName = "Library";
    public const string TagsTabName = "Tags";

    public ObservableCollectionEx<SideNavBarItemViewModel> Tabs { get; }

    public SideNavBarViewModel()
    {
        Tabs = new()
        {
            new()
            {
                Name = SearchTabName,
                ToolTipText = "Search all images",
                Icon = "mdi-magnify",
            },
            new()
            {
                Name = LibraryTabName,
                ToolTipText = "Manage your library",
                Icon = "mdi-bookshelf",
            },
            new()
            {
                Name = TagsTabName,
                ToolTipText = "Manage your tags",
                Icon = "mdi-tag",
            },
        };
    }

    protected override void OnInitialActivated()
    {
        // Set the initial tab
        ChangeTab(SearchTabName);
    }

    [RelayCommand]
    private void ChangeTab(string tabName)
    {
        foreach (var item in Tabs)
        {
            item.IsSelected = false;
        }

        if (Tabs.FirstOrDefault(t => t.Name == tabName) is { } tab)
        {
            tab.IsSelected = true;
        }

        Messenger.Send(new ChangeSideTabMessage(tabName));
    }
}
