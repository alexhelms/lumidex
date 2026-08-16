namespace Lumidex.Features.Settings;

public partial class MainSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ISettingsViewModel SelectedViewModel { get; set; }
    
    public ObservableCollectionEx<ISettingsViewModel> ViewModels { get; }

    public MainSettingsViewModel(
        GeneralSettingsViewModel generalSettings,
        AstrobinSettingsViewModel astrobinSettings,
        SearchSettingsViewModel searchSettings,
        PlotSettingsViewModel plotSettings)
    {
        ViewModels = [
            generalSettings,
            astrobinSettings,
            searchSettings,
            plotSettings,
        ];

        SelectedViewModel = ViewModels[0];
    }
}
