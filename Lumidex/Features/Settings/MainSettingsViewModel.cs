namespace Lumidex.Features.Settings;

public partial class MainSettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ISettingsViewModel SelectedViewModel { get; set; }
    public ObservableCollectionEx<ISettingsViewModel> ViewModels { get; }

    public MainSettingsViewModel(
        AstrobinSettingsViewModel astrobinSettings,
        SearchSettingsViewModel searchSettings)
    {
        ViewModels = [
            astrobinSettings,
            searchSettings,
        ];

        SelectedViewModel = astrobinSettings;
    }
}
