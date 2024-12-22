using Lumidex.Features.Main.Messages;

namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarViewModel : ViewModelBase,
    IRecipient<ChangeSideTab>
{
    // Upper Tabs
    public const string SearchTabName = "Search";
    public const string AliasTabName = "Alias";
    public const string TagsTabName = "Tags";
    public const string LibraryTabName = "Library";
    public const string PlotTabName = "Plot";

    // Lower Tabs
    public const string SettingsTabName = "Settings";

    public ObservableCollectionEx<SideNavBarItemViewModel> UpperTabs { get; }
    public ObservableCollectionEx<SideNavBarItemViewModel> LowerTabs { get; }

    public SideNavBarViewModel()
    {
        UpperTabs = [
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
                Name = PlotTabName,
                ToolTipText = "Plot your data",
                Icon = "mdi-chart-line",
            },
            new()
            {
                Name = LibraryTabName,
                ToolTipText = "Manage your library",
                Icon = "mdi-bookshelf",
            },
        ];

        LowerTabs = [
            new()
            {
                Name = SettingsTabName,
                ToolTipText = "Lumidex Settings",
                Icon = "mdi-cog",
            },
        ];
    }

    protected override void OnInitialActivated()
    {
        // Set the initial tab
        ChangeTab(SearchTabName);
    }

    public void Receive(ChangeSideTab message)
    {
        var allTabs = LowerTabs.Concat(UpperTabs);

        foreach (var item in allTabs)
        {
            item.IsSelected = false;
        }

        if (allTabs.FirstOrDefault(t => t.Name == message.TabName) is { } tab)
        {
            tab.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ChangeTab(string tabName)
    {
        Messenger.Send(new ChangeSideTab(tabName));
    }
}
