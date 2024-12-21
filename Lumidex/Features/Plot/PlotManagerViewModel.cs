namespace Lumidex.Features.Plot;

public partial class PlotManagerViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial List<PlotViewModel> Plots { get; private set; } = [];

    [ObservableProperty]
    public partial PlotViewModel? SelectedPlot { get; set; }

    public PlotManagerViewModel(
        IntegrationOverTimeViewModel integrationOverTime,
        IntegrationPiePlotViewModel integrationPie)
    {
        Plots = [
            integrationOverTime,
            integrationPie
        ];

        SelectedPlot = Plots.First();
    }
}
