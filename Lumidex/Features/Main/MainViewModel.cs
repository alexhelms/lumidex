using Lumidex.Features.MainSearch;
using Lumidex.Features.SideNavBar;
using Lumidex.Messages;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTabMessage>
{
    private readonly Lazy<MainSearchViewModel> _mainSearchViewModel;

    [ObservableProperty] private SideNavBarViewModel? _sideNavBar;
    [ObservableProperty] private object? _selectedTab;

    public MainViewModel(
        SideNavBarViewModel sideNavBar,
        Lazy<MainSearchViewModel> mainSearchViewModel)
    {
        _sideNavBar = sideNavBar;
        _mainSearchViewModel = mainSearchViewModel;
    }

    public void Receive(ChangeSideTabMessage message)
    {
        SelectedTab = message.TabName switch
        {
            SideNavBarViewModel.SearchTabName => _mainSearchViewModel.Value,
            _ => throw new NotImplementedException(),
        };
    }
}
