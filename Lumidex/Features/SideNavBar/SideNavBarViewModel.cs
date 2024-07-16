using Lumidex.Messages;

namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarViewModel : ViewModelBase
{
    public const string SearchTabName = "Search";
    public const string AliasTabName = "Alias";
    public const string TagsTabName = "Tags";
    public const string LibraryTabName = "Library";

    public ObservableCollectionEx<SideNavBarItemViewModel> Tabs { get; }

    public SideNavBarViewModel()
    {
        Tabs = new()
        {
            new()
            {
                Name = SearchTabName,
                ToolTipText = "Search your libraries",
                Icon = "mdi-magnify",
            },
            new()
            {
                Name = AliasTabName,
                ToolTipText = "Manage your aliases",
                Icon = "mdi-at",
            },
            new()
            {
                Name = TagsTabName,
                ToolTipText = "Manage your tags",
                Icon = "mdi-tag",
            },
            new()
            {
                Name = LibraryTabName,
                ToolTipText = "Manage your library",
                Icon = "mdi-bookshelf",
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
