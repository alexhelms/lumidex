namespace Lumidex.Features.Settings;

public partial class MainSettingsViewModel : ViewModelBase
{
    [ObservableProperty] ISettingsViewModel _selectedViewModel;

    public ObservableCollectionEx<ISettingsViewModel> ViewModels { get; }

    public MainSettingsViewModel(
        AstrobinSettingsViewModel astrobinSettings)
    {
        ViewModels = [
            astrobinSettings,
        ];

        SelectedViewModel = astrobinSettings;
    }
}
