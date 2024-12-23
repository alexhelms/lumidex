using Avalonia.Threading;
using Lumidex.Core.Data;
using Microsoft.Data.Sqlite;
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

    [ObservableProperty]
    public partial bool ShowDetailedLabels { get; set; }

    public IntegrationHeatmapViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    partial void OnShowDetailedLabelsChanged(bool value)
    {
        Dispatcher.UIThread.InvokeAsync(DrawPlot);
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
    private void SearchPrev6Months()
    {
        DateBeginLocal = DateTime.Now.AddMonths(-6);
        DrawPlot();
    }

    [RelayCommand]
    private void SearchPrev1Year()
    {
        DateBeginLocal = DateTime.Now.AddYears(-1);
        DrawPlot();
    }

    [RelayCommand]
    private void SearchPrev2Years()
    {
        DateBeginLocal = DateTime.Now.AddYears(-2);
        DrawPlot();
    }

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
        heatmap.CellAlignment = Alignment.MiddleCenter;
        heatmap.Colormap = new ScottPlot.Colormaps.Viridis();

        if (ShowDetailedLabels)
        {
            for (int x = 0, i = 0; x < exposureMap.GetLength(1); x++)
            {
                for (int y = 0; y < exposureMap.GetLength(0); y++, i++)
                {
                    Coordinates coordinates = new(x, (DaysInAWeek - 1) - y);
                    var value = exposureMap[y, x];
                    if (value == 0) continue;
                    var cellLabel = $"{value:F1}\n{start.AddDays(i):M-d}";
                    var text = Plot.Add.Text(cellLabel, coordinates);
                    text.Alignment = Alignment.MiddleCenter;
                    text.LabelFontColor = Colors.White;
                    text.LabelFontSize = 8;
                }
            }
        }

        _colorBar = Plot.Add.ColorBar(heatmap, Edge.Bottom);
        _colorBar.Label = "Exposure (hr)";
        _colorBar.LabelStyle.ForeColor = Colors.White;
        _colorBar.Axis.TickLabelStyle.ForeColor = Colors.White;
        _colorBar.Axis.FrameLineStyle.IsVisible = false;
        _colorBar.Axis.MajorTickStyle.Color = Colors.Transparent;
        _colorBar.Axis.MinorTickStyle.Color = Colors.Transparent;

        Plot.HideGrid();
        Plot.Axes.SquareUnits((end - start).TotalDays > 90);
        Plot.Axes.Margins();
        Plot.Axes.FrameColor(Colors.Transparent);
        Plot.Axes.Left.SetTicks([], []);
        Plot.Axes.Bottom.SetTicks([], []);

        // Day labels
        var sunday = DateTime.Now.AddDays(DayOfWeek.Sunday - DateTime.Now.DayOfWeek);
        for (int i = 0; i < DaysInAWeek; i++)
        {
            var text = Plot.Add.Text(sunday.AddDays(DaysInAWeek - i - 1).ToString("ddd"), -0.5, i);
            text.LabelFontColor = Colors.White;
            text.LabelAlignment = Alignment.MiddleRight;
            text.OffsetX = -5;
        }

        // Month labels
        int monthCounter = 0;
        for (int i = 0; i < numDays; i++)
        {
            var today = start.AddDays(i);
            
            // Every week, with a special case for the first cell in the first column being the first of the month.
            if (((i + 1) % DaysInAWeek == 0) || (i == 0 && today.Day == 1))
            {
                var week = start.AddDays(i == 0 ? i : i + 1);

                // Every month
                if (week.Month != week.AddDays(-DaysInAWeek).Month)
                {
                    // Limit the labels when there are multiple years
                    if (monthCounter % Math.Max(1, end.Year - start.Year) == 0)
                    {
                        var label = week.ToString("MMM |yy").Replace('|', '\'');
                        var text = Plot.Add.Text(label, i / DaysInAWeek, -1);
                        text.LabelFontColor = Colors.White;
                        text.LabelAlignment = Alignment.UpperCenter;
                        text.LabelOffsetX = 8;
                        text.LabelOffsetY = -5;
                    }

                    monthCounter++;
                }
            }
        }

        // Border
        var heatmapBorder = Plot.Add.Rectangle(-0.5, numWeeks - 0.5, -0.5, DaysInAWeek - 0.5);
        heatmapBorder.LineColor = Colors.White;
        heatmapBorder.FillColor = Colors.Transparent;

        RefreshPlot();
    }

    private Dictionary<DateTime, double> GetPlotData()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var end = DateEndLocal.ToString("yyyy-MM-dd");
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

        var parameters = new List<SqliteParameter>();

        var libraryFilter = "AND 1 = 1";
        if (Library is not null)
        {
            libraryFilter = "AND LibraryId = @libraryId";
            parameters.Add(new SqliteParameter("@libraryId", Library.Id));
        }

        var cameraNameFilter = "AND 1 = 1";
        if (!string.IsNullOrWhiteSpace(CameraName))
        {
            cameraNameFilter = "AND CameraName LIKE @cameraName";
            parameters.Add(new SqliteParameter("@cameraName", $"%{CameraName}%"));
        }

        var telescopeNameFilter = "AND 1 = 1";
        if (!string.IsNullOrWhiteSpace(TelescopeName))
        {
            cameraNameFilter = "AND TelescopeName LIKE @telescopeName";
            parameters.Add(new SqliteParameter("@telescopeName", $"%{TelescopeName}%"));
        }

        var sql =
            $"""
            SELECT DISTINCT 
                Timestamp, 
                SUM(Exposure)/3600 AS TotalExposure
            FROM (
                SELECT strftime('%Y-%m-%d', ObservationTimestampLocal, '-12:00') as Timestamp, Exposure
                FROM ImageFiles
                WHERE 1 = 1
                    AND Type = @type
                    AND Kind = @kind
                    AND ObservationTimestampLocal IS NOT NULL
                    AND strftime('%Y-%m-%d', ObservationTimestampLocal, '-12:00') >= @start
                    AND strftime('%Y-%m-%d', ObservationTimestampLocal, '-12:00') <= @end
                    {libraryFilter}
                    {cameraNameFilter}
                    {telescopeNameFilter}
            )
            GROUP BY Timestamp
            ORDER BY Timestamp
            """;

        parameters.AddRange([
            new SqliteParameter("@type", type),
            new SqliteParameter("@kind", kind),
            new SqliteParameter("@start", start),
            new SqliteParameter("@end", end),
        ]);

        var items = dbContext.Database
            .SqlQueryRaw<ExposureGroup>(sql, parameters.ToArray())
            .ToDictionary(group => group.Timestamp, group => group.TotalExposure);

        return items;
    }

    private record ExposureGroup(DateTime Timestamp, double TotalExposure);
}
