using Lumidex.Features.Tags;
using Lumidex.Features.Library;
using Lumidex.Features.MainSearch;
using Lumidex.Features.SideNavBar;
using Lumidex.Messages;
using Lumidex.Features.Aliases;
using Avalonia.Controls.ApplicationLifetimes;
using Lumidex.Services;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTabMessage>
{
    private readonly SystemService _systemService;
    private readonly DialogService _dialogService;
    private readonly AboutViewModel _aboutViewModel;
    private readonly MainSearchViewModel _mainSearchViewModel;
    private readonly AliasManagerViewModel _aliasManagerViewModel;
    private readonly TagManagerViewModel _tagManagerViewModel;
    private readonly LibraryManagerViewModel _libraryManagerViewModel;

    [ObservableProperty] private SideNavBarViewModel _sideNavBarViewModel = null!;
    [ObservableProperty] private object? _selectedTab;

    public MainViewModel(
        SystemService systemService,
        DialogService dialogService,
        AboutViewModel aboutViewModel,
        SideNavBarViewModel sideNavBarViewModel,
        MainSearchViewModel mainSearchViewModel,
        AliasManagerViewModel aliasManagerViewModel,
        TagManagerViewModel tagManagerViewModel,
        LibraryManagerViewModel libraryManagerViewModel)
    {
        _systemService = systemService;
        _dialogService = dialogService;
        _aboutViewModel = aboutViewModel;
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

    [RelayCommand]
    private void ExitApplication()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
        {
            desktopApp.Shutdown();
        }
    }

    [RelayCommand]
    private Task ReportBug() => _systemService.OpenUrl("https://github.com/alexhelms/lumidex/issues/new?labels=bug&template=bug_report.md&projects=Lumidex");

    [RelayCommand]
    private Task RequestFeature() => _systemService.OpenUrl("https://github.com/alexhelms/lumidex/issues/new?labels=enhancement&template=feature_request.md&projects=Lumidex");

    [RelayCommand]
    private Task OpenAboutDialog() => _dialogService.ShowDialog(_aboutViewModel);
}
