using Lumidex.Messages;

namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarViewModel : ViewModelBase
{
    // Upper Tabs
    public const string SearchTabName = "Search";
    public const string AliasTabName = "Alias";
    public const string TagsTabName = "Tags";
    public const string LibraryTabName = "Library";

    // Lower Tabs
    public const string SettingsTabName = "Settings";

    public ObservableCollectionEx<SideNavBarItemViewModel> UpperTabs { get; }
    public ObservableCollectionEx<SideNavBarItemViewModel> LowerTabs { get; }

    public SideNavBarViewModel()
    {
        UpperTabs = new()
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

        LowerTabs = new()
        {
            new()
            {
                Name = SettingsTabName,
                ToolTipText = "Lumidex Settings",
                Icon = "mdi-cog",
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
        var allTabs = LowerTabs.Concat(UpperTabs);

        foreach (var item in allTabs)
        {
            item.IsSelected = false;
        }

        if (allTabs.FirstOrDefault(t => t.Name == tabName) is { } tab)
        {
            tab.IsSelected = true;
        }

        Messenger.Send(new ChangeSideTabMessage(tabName));
    }
}
