using ScottPlot;

namespace Lumidex.Features.Plot;

public abstract partial class PlotViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ScottPlot.Plot Plot { get; private set; } = null!;
    
    public abstract string DisplayName { get; }

    protected PlotViewModel()
    {
        Plot = new();
        ApplyPlotStyle();
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();
    }

    protected void ApplyPlotStyle()
    {
        Plot.Add.Palette = new ScottPlot.Palettes.Penumbra();
        Plot.FigureBackground.Color = Color.FromHex("#1e1f22");
        Plot.DataBackground.Color = Color.FromHex("#1e1f22");
        Plot.Axes.Color(Color.FromHex("#ffffff"));
        Plot.Grid.MajorLineColor = Color.FromHex("#656565");
        Plot.Legend.BackgroundColor = Color.FromHex("#656565");
        Plot.Legend.FontColor = Color.FromHex("#656565");
        Plot.Legend.OutlineColor = Color.FromHex("#656565");
    }

    public void RefreshPlot()
    {
        Plot.PlotControl?.Refresh();
    }
}
