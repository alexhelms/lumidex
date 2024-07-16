using Lumidex.Features.Tags;
using Lumidex.Features.Library;
using Lumidex.Features.MainSearch;
using Lumidex.Features.SideNavBar;
using Lumidex.Messages;
using Lumidex.Features.Aliases;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTabMessage>
{
    private readonly MainSearchViewModel _mainSearchViewModel;
    private readonly AliasManagerViewModel _aliasManagerViewModel;
    private readonly TagManagerViewModel _tagManagerViewModel;
    private readonly LibraryManagerViewModel _libraryManagerViewModel;

    [ObservableProperty] private SideNavBarViewModel _sideNavBarViewModel = null!;
    [ObservableProperty] private object? _selectedTab;

    public MainViewModel(
        SideNavBarViewModel sideNavBarViewModel,
        MainSearchViewModel mainSearchViewModel,
        AliasManagerViewModel aliasManagerViewModel,
        TagManagerViewModel tagManagerViewModel,
        LibraryManagerViewModel libraryManagerViewModel)
    {
        _sideNavBarViewModel = sideNavBarViewModel;
        _mainSearchViewModel = mainSearchViewModel;
        _aliasManagerViewModel = aliasManagerViewModel;
        _tagManagerViewModel = tagManagerViewModel;
        _libraryManagerViewModel = libraryManagerViewModel;
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
            SideNavBarViewModel.AliasTabName => _aliasManagerViewModel,
            SideNavBarViewModel.TagsTabName => _tagManagerViewModel,
            SideNavBarViewModel.LibraryTabName => _libraryManagerViewModel,
            _ => throw new NotImplementedException(),
        };
    }
}
