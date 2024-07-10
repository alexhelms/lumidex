using Lumidex.Features.Tags;
using Lumidex.Features.Library;
using Lumidex.Features.MainSearch;
using Lumidex.Features.SideNavBar;
using Lumidex.Messages;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTabMessage>
{
    private readonly MainSearchViewModel _mainSearchViewModel;
    private readonly LibraryManagerViewModel _libraryManagerViewModel;
    private readonly TagManagerViewModel _tagManagerViewModel;

    [ObservableProperty] private SideNavBarViewModel _sideNavBarViewModel = null!;
    [ObservableProperty] private object? _selectedTab;

    public MainViewModel(
        SideNavBarViewModel sideNavBarViewModel,
        MainSearchViewModel mainSearchViewModel,
        LibraryManagerViewModel libraryManagerViewModel,
        TagManagerViewModel tagManagerViewModel)
    {
        _sideNavBarViewModel = sideNavBarViewModel;
        _mainSearchViewModel = mainSearchViewModel;
        _libraryManagerViewModel = libraryManagerViewModel;
        _tagManagerViewModel = tagManagerViewModel;
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        // HACK: activate each child so they have a chance to initialize
        ToggleActivation(SideNavBarViewModel);
        ToggleActivation(_mainSearchViewModel);
        ToggleActivation(_libraryManagerViewModel);
        ToggleActivation(_tagManagerViewModel);

        static void ToggleActivation(IActivatable vm)
        {
            vm.IsActive = true;
            vm.IsActive = false;
        }
    }

    public void Receive(ChangeSideTabMessage message)
    {
        SelectedTab = message.TabName switch
        {
            SideNavBarViewModel.SearchTabName => _mainSearchViewModel,
            SideNavBarViewModel.LibraryTabName => _libraryManagerViewModel,
            SideNavBarViewModel.TagsTabName => _tagManagerViewModel,
            _ => throw new NotImplementedException(),
        };
    }
}
