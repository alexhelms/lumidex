using Lumidex.Features.Tags;
using Lumidex.Features.Library;
using Lumidex.Features.MainSearch;
using Lumidex.Features.SideNavBar;
using Lumidex.Messages;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTabMessage>
{
    private readonly Lazy<MainSearchViewModel> _mainSearch;
    private readonly Lazy<LibraryManagerViewModel> _libraryManager;
    private readonly Lazy<TagManagerViewModel> _tagManager;

    [ObservableProperty] private SideNavBarViewModel? _sideNavBar;
    [ObservableProperty] private object? _selectedTab;

    public MainViewModel(
        SideNavBarViewModel sideNavBar,
        Lazy<MainSearchViewModel> mainSearch,
        Lazy<LibraryManagerViewModel> libraryManager,
        Lazy<TagManagerViewModel> tagManager)
    {
        _sideNavBar = sideNavBar;
        _mainSearch = mainSearch;
        _libraryManager = libraryManager;
        _tagManager = tagManager;
    }

    public void Receive(ChangeSideTabMessage message)
    {
        SelectedTab = message.TabName switch
        {
            SideNavBarViewModel.SearchTabName => _mainSearch.Value,
            SideNavBarViewModel.LibraryTabName => _libraryManager.Value,
            SideNavBarViewModel.TagsTabName => _tagManager.Value,
            _ => throw new NotImplementedException(),
        };
    }
}
