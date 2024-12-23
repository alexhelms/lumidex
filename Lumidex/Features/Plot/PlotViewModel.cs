using Avalonia.Threading;
using Lumidex.Features.Library.Messages;
using ScottPlot;

namespace Lumidex.Features.Plot;

public abstract partial class PlotViewModel : ViewModelBase,
    IRecipient<LibraryCreated>,
    IRecipient<LibraryDeleted>,
    IRecipient<LibraryEdited>
{
    [ObservableProperty]
    public partial ScottPlot.Plot Plot { get; private set; } = null!;

    [ObservableProperty]
    public partial LibraryViewModel? Library { get; set; }

    [ObservableProperty]
    public partial ObservableCollectionEx<LibraryViewModel> Libraries { get; set; } = [];

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

    [RelayCommand]
    private void ClearLibrary() => Library = null;

    public void RefreshPlot()
    {
        Plot.PlotControl?.Refresh();
    }

    public void Receive(LibraryCreated message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Libraries.Contains(message.Library))
            {
                Libraries.Add(message.Library);
            }
        });
    }

    public void Receive(LibraryDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Libraries.Remove(message.Library);
        });
    }

    public void Receive(LibraryEdited message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {

            if (Libraries.FirstOrDefault(library => library == message.Library) is { } existingLibrary)
            {
                // Retain a reference to the selected library, if one exists. Removing it from the list deselects!
                LibraryViewModel? selectedLibrary = Library;

                var index = Libraries.IndexOf(existingLibrary);
                Libraries.RemoveAt(index);
                Libraries.Insert(index, message.Library);

                // Retain user selection
                if (selectedLibrary is not null && selectedLibrary == message.Library)
                    Library = message.Library;
            }
        });
    }
}
