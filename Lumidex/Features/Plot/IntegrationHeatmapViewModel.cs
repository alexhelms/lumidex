using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using ScottPlot;

namespace Lumidex.Features.Plot;

public partial class IntegrationHeatmapViewModel : PlotViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    private ScottPlot.Panels.ColorBar? _colorBar;

    public override string DisplayName => "Integration Heatmap";

    [ObservableProperty]
    public partial DateTime DateBeginLocal { get; set; }

    [ObservableProperty]
    public partial DateTime DateEndLocal { get; set; }

    [ObservableProperty]
    public partial string? CameraName { get; set; }

    [ObservableProperty]
    public partial string? TelescopeName { get; set; }

    public IntegrationHeatmapViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        DateBeginLocal = DateTime.Now.AddYears(-1);
        DateEndLocal = DateTime.Now;

        GeneratePlot();
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
        if (_colorBar is not null)
        {
            Plot.Axes.Remove(_colorBar);
        }

        Plot.Clear();
        var data = GetPlotData();

        const int DaysInAWeek = 7;
        const int DaysInAMonth = 30;
        DateTime end = DateEndLocal.Date;
        DateTime start = DateBeginLocal.Date;
        if (end < start)
        {
            var tmp = start;
            start = end;
            end = tmp;
        }    

        int numDays = (int)(end - start).TotalDays;
        numDays = (int)Math.Round(Math.Ceiling(numDays / (double)DaysInAWeek)) * DaysInAWeek;
        int numWeeks = Math.Max(1, numDays / DaysInAWeek);
        int numMonths = Math.Max(1, numDays / DaysInAMonth);

        double[,] exposureMap = new double[DaysInAWeek, numWeeks];
        for (int week = 0; week < numWeeks; week++)
        {
            for (int day = 0; day < DaysInAWeek; day++)
            {
                var date = start.AddDays(DaysInAWeek * week + day);
                data.TryGetValue(date, out double exposure);
                exposureMap[day, week] = exposure;
            }
        }

        var heatmap = Plot.Add.Heatmap(exposureMap);
        heatmap.CellAlignment = Alignment.MiddleLeft;
        heatmap.Colormap = new ScottPlot.Colormaps.Viridis();

        _colorBar = Plot.Add.ColorBar(heatmap, Edge.Bottom);
        _colorBar.Label = "Exposure (hr)";
        _colorBar.LabelStyle.ForeColor = Colors.White;
        _colorBar.Axis.TickLabelStyle.ForeColor = Colors.White;
        _colorBar.Axis.FrameLineStyle.IsVisible = false;
        _colorBar.Axis.MajorTickStyle.Color = Colors.Transparent;
        _colorBar.Axis.MinorTickStyle.Color = Colors.Transparent;

        Plot.HideGrid();
        //Plot.Layout.Frameless();
        Plot.Axes.SquareUnits();
        Plot.Axes.Left.Min = 0;
        Plot.Axes.Bottom.Min = 0;
        Plot.Axes.Margins(0.06, 0);
        Plot.Axes.FrameColor(Colors.Transparent);
        Plot.Axes.Left.SetTicks([], []);
        Plot.Axes.Bottom.SetTicks([], []);

        // Day labels
        var sunday = DateTime.Now.AddDays(DayOfWeek.Sunday - DateTime.Now.DayOfWeek);
        for (int i = 0; i < DaysInAWeek; i++)
        {
            var text = Plot.Add.Text(sunday.AddDays(DaysInAWeek - i - 1).ToString("ddd"), 0, i);
            text.LabelFontColor = Colors.White;
            text.LabelAlignment = Alignment.MiddleRight;
            text.OffsetX = -5;
        }

        // Month labels
        for (int i = 0; i <= numMonths; i++)
        {
            var label = start.AddMonths(i).ToString("MMM |yy").Replace('|', '\'');
            var text = Plot.Add.Text(label, i * (numWeeks / numMonths), -1);
            text.LabelFontColor = Colors.White;
            text.LabelAlignment = Alignment.UpperCenter;
            text.LabelOffsetX = 30;
            text.LabelOffsetY = -5;
        }

        // Border
        var heatmapBorder = Plot.Add.Rectangle(0, numWeeks, -0.5, DaysInAWeek - 0.5);
        heatmapBorder.LineColor = Colors.White;
        heatmapBorder.FillColor = Colors.Transparent;

        RefreshPlot();
    }

    private Dictionary<DateTime, double> GetPlotData()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        // sqlite implicitly selects the first of the month (I think)
        // so I need to add an additional month so the WHERE clause
        // works as expected.
        var end = DateEndLocal.AddDays(1).ToString("yyyy-MM-dd");
        var start = DateBeginLocal.ToString("yyyy-MM-dd");
        var type = (int)ImageType.Light;
        var kind = (int)ImageKind.Raw;
        bool hasCameraName = !string.IsNullOrWhiteSpace(CameraName);
        bool hasTelescopeName = !string.IsNullOrWhiteSpace(TelescopeName);

        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            if (settings.UseCalibratedFrames)
            {
                kind = (int)ImageKind.Calibration;
            }
        }

        FormattableString cameraNameFilter = $"1 = 1";
        if (!string.IsNullOrWhiteSpace(CameraName))
        {
            cameraNameFilter = $"CameraName LIKE '%{CameraName}%'";
        }

        FormattableString telescopeNameFilter = $"1 = 1";
        if (!string.IsNullOrWhiteSpace(TelescopeName))
        {
            telescopeNameFilter = $"TelescopeName LIKE '%{TelescopeName}%'";
        }

        FormattableString sql =
            $"""
            SELECT DISTINCT 
                strftime('%Y-%m-%d', ObservationTimestampLocal, '-12:00') as Timestamp, 
                SUM(Exposure)/3600 AS TotalExposure
            FROM (
                SELECT ObservationTimestampLocal, Exposure
                FROM ImageFiles
                WHERE 1 = 1
                    AND Type = {type}
                    AND Kind = {kind}
                    AND ObservationTimestampLocal IS NOT NULL
                    AND ObservationTimestampLocal > {start}
                    AND ObservationTimestampLocal < {end}
                    AND {cameraNameFilter.ToString()}
                    AND {telescopeNameFilter.ToString()}
            )
            GROUP BY Timestamp
            ORDER BY Timestamp
            """;
        
        var items = dbContext.Database
            .SqlQuery<ExposureGrouping>(sql)
            .ToDictionary(group => group.Timestamp, group => group.TotalExposure);

        return items;
    }

    private record ExposureGrouping(DateTime Timestamp, double TotalExposure);
}
