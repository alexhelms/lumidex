using Humanizer;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using ScottPlot;

namespace Lumidex.Features.Plot;

public partial class IntegrationPiePlotViewModel : PlotViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public override string DisplayName => "Integration Pie";

    [ObservableProperty]
    public partial DateTime DateBeginLocal { get; set; }

    [ObservableProperty]
    public partial DateTime DateEndLocal { get; set; }

    public IntegrationPiePlotViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        DateBeginLocal = DateTime.Now.AddMonths(-3);
        DateEndLocal = DateTime.Now;

        GeneratePlot();
    }

    [RelayCommand]
    private void DrawPlot()
    {
        GeneratePlot();
    }

    private void GeneratePlot()
    {
        Plot.Clear();

        var data = GetPlotData();
        var slices = new List<PieSlice>(data.Count);
        var sum = data.Values.Sum();
        var threshold = 0.02 * sum;
        var colors = Rainbow(data.Count).ToArray();
        int i = 0;

        foreach (var (name, totalExposure) in data)
        {
            if (totalExposure >= threshold)
            {
                slices.Add(new PieSlice
                {
                    Label = $"{name}{Environment.NewLine}{totalExposure:F1} hr",
                    Value = totalExposure,
                    LabelFontColor = Colors.White,
                    LabelFontSize = 16,
                    FillColor = colors[i],
                });
            }

            i++;
        }

        var pie = Plot.Add.Pie(slices);
        pie.SliceLabelDistance = 1.2;
        pie.LineColor = Color.FromHex("#1e1f22");
        pie.LineWidth = 2;
        pie.DonutFraction = 0.25;

        Plot.Title("Integration Pie");
        Plot.HideLegend();
        Plot.HideAxesAndGrid();
        Plot.Axes.Margins(0, 0);
        Plot.Axes.AutoScale();

        RefreshPlot();
    }

    private Dictionary<string, double> GetPlotData()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var comparer = StringComparer.OrdinalIgnoreCase;
        var kind = ImageKind.Raw;
        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            if (settings.UseCalibratedFrames)
            {
                kind = ImageKind.Calibration;
            }
        }

        // Naive query of total exposure grouped by object name, not considering object name aliases.
        Dictionary<string, double> groups = dbContext.ImageFiles
            .AsNoTracking()
            .Where(image => image.ObjectName != null)
            .Where(image => image.Exposure != null)
            .Where(image => image.Type == ImageType.Light)
            .Where(image => image.Kind == kind)
            .Where(image => image.ObservationTimestampLocal >= DateBeginLocal)
            .Where(image => image.ObservationTimestampLocal <= DateEndLocal)
            .GroupBy(image => image.ObjectName!)
            .Select(group => new
            {
                ObjectName = group.Key,
                TotalExposure = (double)group.Sum(x => x.Exposure)! / 3600.0,
            })
            .ToDictionary(group => group.ObjectName, group => group.TotalExposure!, comparer);

        return groups;
    }

    public static IEnumerable<Color> Rainbow(int count)
    {
        for (int i = 0; i < count; i++)
        {
            float hue = (float)i / count;
            float saturation = 1.0f;
            float luminosity = 0.6f;
            yield return Color.FromHSL(hue, saturation, luminosity);
        }
    }
}
