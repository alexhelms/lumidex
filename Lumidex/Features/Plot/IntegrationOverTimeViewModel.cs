using Lumidex.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScottPlot;
using ScottPlot.TickGenerators;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Lumidex.Features.Plot;

public partial class IntegrationOverTimeViewModel : PlotViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public override string DisplayName => "Integration Over Time";

    [ObservableProperty]
    public partial DateTime DateBeginLocal { get; set; }

    [ObservableProperty]
    public partial DateTime DateEndLocal { get; set; }

    [ObservableProperty]
    public partial string? CameraName { get; set; }

    [ObservableProperty]
    public partial string? TelescopeName { get; set; }

    public IntegrationOverTimeViewModel(IDbContextFactory<LumidexDbContext> dbContextFactory)
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
        Plot.Clear();
        var (xValues, yValues) = GetPlotData();

        var barPlot = Plot.Add.Bars(xValues, yValues);
        foreach (var bar in barPlot.Bars)
        {
            bar.Label = bar.Value.ToString("F1");
            bar.Size = 20;
            bar.FillColor = Color.FromHex("#005496");
        }

        barPlot.ValueLabelStyle.Bold = true;
        barPlot.ValueLabelStyle.FontSize = 18;
        barPlot.ValueLabelStyle.ForeColor = Colors.White;

        var dateAxis = Plot.Axes.DateTimeTicksBottom();
        dateAxis.TickLabelStyle.Rotation = 30;
        dateAxis.TickLabelStyle.Alignment = Alignment.UpperLeft;
        dateAxis.TickGenerator = new DateTimeFixedInterval(
            interval: new ScottPlot.TickGenerators.TimeUnits.Month(),
            intervalsPerTick: 1,
            getIntervalStartFunc: dt => new DateTime(dt.Year, dt.Month, 1));

        Plot.RenderManager.RenderStarting += (s, e) =>
        {
            Tick[] ticks = Plot.Axes.Bottom.TickGenerator.Ticks;
            for (int i = 0; i < ticks.Length; i++)
            {
                var dt = DateTime.FromOADate(ticks[i].Position);
                var label = dt.ToString("yyyy-MM");
                ticks[i] = new Tick(ticks[i].Position, label);
            }
        };

        Plot.Layout.Fixed(new PixelPadding(60, 48, 60, 52));
        Plot.Title($"Integration Over Time");
        Plot.Axes.Left.Label.Text = "Hours";
        Plot.Axes.Left.Min = 0;

        // By subtracting a month but adding one day, we prevent the first X axis label from rendering.
        Plot.Axes.Bottom.Min = DateBeginLocal.AddMonths(-1).AddDays(1).ToOADate();
        Plot.Axes.Bottom.Max = DateEndLocal.ToOADate();

        Plot.Axes.SetLimitsY(0, Math.Max(Math.Max(yValues.Max(), 1) * 1.1, 10));

        ApplyPlotStyle();
        RefreshPlot();
    }

    private (double[], double[]) GetPlotData()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        var start = DateBeginLocal.ToString("yyyy-MM");
        var end = DateEndLocal.ToString("yyyy-MM");
        var type = (int)ImageType.Light;
        var kind = (int)ImageKind.Raw;

        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            if (settings.UseIntermediateFramesForPlots)
            {
                kind = (int)ImageKind.Intermediate;
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
            	SELECT strftime('%Y-%m', ObservationTimestampLocal, '-12:00') as Timestamp, Exposure
            	FROM ImageFiles
            	WHERE 1 = 1
                    AND Type = @type
                    AND Kind = @kind
                    AND ObservationTimestampLocal IS NOT NULL
                    AND strftime('%Y-%m', ObservationTimestampLocal, '-12:00') >= @start
                    AND strftime('%Y-%m', ObservationTimestampLocal, '-12:00') <= @end
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
            .ToList();

        double[] xValues = items.Select(item => item.Timestamp.ToOADate()).ToArray();
        double[] yValues = items.Select(item => item.TotalExposure).ToArray();

        // If no data is available, create some fake data to plot so "no data" renders a reasonable plot.
        if (xValues.Length == 0 && yValues.Length == 0)
        {
            var now = DateTime.Now;
            now = new DateTime(now.Year, now.Month, 1);

            int length = 12;
            xValues = new double[length];
            yValues = new double[length];

            for (int i = 0; i < length; i++)
            {
                xValues[i] = now.AddMonths(i + 1).ToOADate();
                yValues[i] = 0;
            }
        }

        return (xValues, yValues);
    }

    public record ExposureGroup(DateTime Timestamp, double TotalExposure);
}
