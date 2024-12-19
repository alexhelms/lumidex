using Avalonia.Controls.ApplicationLifetimes;
using Lumidex.Core;
using Lumidex.Core.IO;
using Lumidex.Features.Aliases;
using Lumidex.Features.Library;
using Lumidex.Features.Main.Messages;
using Lumidex.Features.MainSearch;
using Lumidex.Features.Plot;
using Lumidex.Features.Settings;
using Lumidex.Features.SideNavBar;
using Lumidex.Features.Tags;
using Lumidex.Services;

namespace Lumidex.Features.Main;

public partial class MainViewModel : ViewModelBase,
    IRecipient<ChangeSideTab>
{
    private readonly SystemService _systemService;
    private readonly DialogService _dialogService;
    private readonly AboutViewModel _aboutViewModel;
    private readonly MainSearchViewModel _mainSearchViewModel;
    private readonly AliasManagerViewModel _aliasManagerViewModel;
    private readonly TagManagerViewModel _tagManagerViewModel;
    private readonly LibraryManagerViewModel _libraryManagerViewModel;
    private readonly PlotManagerViewModel _plotManagerViewModel;
    private readonly MainSettingsViewModel _settingsViewModel;

    [ObservableProperty]
    public partial SideNavBarViewModel SideNavBarViewModel { get; set; } = null!;

    [ObservableProperty]
    public partial object? SelectedTab { get; set; }

    [ObservableProperty]
    public partial int TopBarHeight { get; set; }

    public MainViewModel(
        SystemService systemService,
        DialogService dialogService,
        AboutViewModel aboutViewModel,
        SideNavBarViewModel sideNavBarViewModel,
        MainSearchViewModel mainSearchViewModel,
        AliasManagerViewModel aliasManagerViewModel,
        TagManagerViewModel tagManagerViewModel,
        LibraryManagerViewModel libraryManagerViewModel,
        PlotManagerViewModel plotManagerViewModel,
        MainSettingsViewModel settingsViewModel)
    {
        _systemService = systemService;
        _dialogService = dialogService;
        _aboutViewModel = aboutViewModel;
        SideNavBarViewModel = sideNavBarViewModel;
        _mainSearchViewModel = mainSearchViewModel;
        _aliasManagerViewModel = aliasManagerViewModel;
        _tagManagerViewModel = tagManagerViewModel;
        _libraryManagerViewModel = libraryManagerViewModel;
        _plotManagerViewModel = plotManagerViewModel;
        _settingsViewModel = settingsViewModel;

        // 28px for MacOS so traffic light is vertically centered.
        TopBarHeight = LumidexUtil.IsMacOS ? 28 : 30;
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

    public void Receive(ChangeSideTab message)
    {
        SelectedTab = message.TabName switch
        {
            // Upper Tabs
            SideNavBarViewModel.SearchTabName => _mainSearchViewModel,
            SideNavBarViewModel.AliasTabName => _aliasManagerViewModel,
            SideNavBarViewModel.TagsTabName => _tagManagerViewModel,
            SideNavBarViewModel.LibraryTabName => _libraryManagerViewModel,
            SideNavBarViewModel.PlotTabName => _plotManagerViewModel,
            // Lower Tabs
            SideNavBarViewModel.SettingsTabName => _settingsViewModel,
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

    [RelayCommand]
    private Task OpenLogFolder() => _systemService.OpenInExplorer(LumidexPaths.Logs);
}
