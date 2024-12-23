using Avalonia.Threading;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using ScottPlot;
using Timer = System.Timers.Timer;

namespace Lumidex.Features.Plot;

public partial class IntegrationPiePlotViewModel : PlotViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    private Timer? _timer;

    public override string DisplayName => "Integration Pie";

    [ObservableProperty]
    public partial DateTime DateBeginLocal { get; set; }

    [ObservableProperty]
    public partial DateTime DateEndLocal { get; set; }

    [ObservableProperty]
    public partial string? CameraName { get; set; }

    [ObservableProperty]
    public partial string? TelescopeName { get; set; }

    [ObservableProperty]
    public partial double CutoffThreshold { get; set; } = 2;

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

    protected override void OnActivated()
    {
        base.OnActivated();
        _timer = new System.Timers.Timer(TimeSpan.FromMilliseconds(50));
        _timer.AutoReset = false;
        _timer.Elapsed += (_, _) =>
        {
            if (IsActive)
            {
                Dispatcher.UIThread.InvokeAsync(GeneratePlot);
            }
        };
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    partial void OnCutoffThresholdChanged(double value)
    {
        _timer?.Stop();
        _timer?.Start();
    }

    [RelayCommand]
    private void ClearCameraName() => CameraName = null;

    [RelayCommand]
    private void ClearTelescopeName() => TelescopeName = null;

    [RelayCommand]
    private void DrawPlot()
    {
        GeneratePlot();
    }

    private void GeneratePlot()
    {
        Plot.Clear();

        Dictionary<string, double> data = GetPlotData();
        var slices = new List<PieSlice>(data.Count);
        var sum = data.Values.Sum();
        var threshold = CutoffThreshold / 100.0 * sum;
        bool sliceRemoved = false;

        do
        {
            sliceRemoved = false;
            sum = data.Values.Sum();
            threshold = CutoffThreshold / 100.0 * sum;
            foreach (var (name, totalExposure) in data)
            {
                if (totalExposure <= threshold)
                {
                    data.Remove(name);
                    sliceRemoved = true;
                    break;
                }
            }
        }
        while (sliceRemoved);

        var colors = Rainbow(data.Count).ToArray();
        int colorIndex = 0;

        foreach (var (name, totalExposure) in data)
        {
            slices.Add(new PieSlice
            {
                Label = $"{name}{Environment.NewLine}{totalExposure:F1} hr",
                Value = totalExposure,
                LabelFontColor = Colors.White,
                LabelFontSize = 16,
                FillColor = colors[colorIndex],
            });

            colorIndex++;
        }

        var pie = Plot.Add.Pie(slices);
        pie.SliceLabelDistance = 1.2;
        pie.LineColor = Color.FromHex("#1e1f22");
        pie.LineWidth = 2;
        pie.DonutFraction = 0.25;

        var totalAnnotation = Plot.Add.Annotation($"{sum:F1} hours", Alignment.MiddleCenter);
        totalAnnotation.LabelFontSize = 20;
        totalAnnotation.LabelFontColor = Colors.White;
        totalAnnotation.LabelBackgroundColor = Colors.Transparent;
        totalAnnotation.LabelBorderColor = Colors.Transparent;
        totalAnnotation.LabelShadowColor = Colors.Transparent;

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
            if (settings.UseIntermediateFramesForPlots)
            {
                kind = ImageKind.Intermediate;
            }
        }

        var query = dbContext.ImageFiles
            .AsNoTracking()
            .Where(image => image.ObjectName != null)
            .Where(image => image.Exposure != null)
            .Where(image => image.Type == ImageType.Light)
            .Where(image => image.Kind == kind)
            .Where(image => image.ObservationTimestampLocal >= DateBeginLocal)
            .Where(image => image.ObservationTimestampLocal <= DateEndLocal)
            .AsQueryable();

        if (Library is not null)
            query = query.Where(f => f.LibraryId == Library.Id);

        if (!string.IsNullOrWhiteSpace(CameraName))
            query = query.Where(f => EF.Functions.Like(f.CameraName, $"%{CameraName}%"));

        if (!string.IsNullOrWhiteSpace(TelescopeName))
            query = query.Where(f => EF.Functions.Like(f.TelescopeName, $"%{TelescopeName}%"));

        Dictionary<string, double> groups = query
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
