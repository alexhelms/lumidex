using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using ScottPlot;
using ScottPlot.TickGenerators;

namespace Lumidex.Features.Plot;

public partial class IntegrationOverTimeViewModel : PlotViewModel
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public override string DisplayName => "Integration Over Time";

    [ObservableProperty]
    public partial DateTime DateBeginLocal { get; set; }

    [ObservableProperty]
    public partial DateTime DateEndLocal { get; set; }

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
        Plot.Axes.SetLimitsY(0, Math.Max(Math.Max(yValues.Max(), 1) * 1.1, 10));

        ApplyPlotStyle();
        RefreshPlot();
    }

    private (double[], double[]) GetPlotData()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        // sqlite implicitly selects the first of the month (I think)
        // so I need to add an additional month so the WHERE clause
        // works as expected.
        var end = DateEndLocal.AddMonths(1).ToString("yyyy-MM");
        var start = DateBeginLocal.ToString("yyyy-MM");
        var type = (int)ImageType.Light;
        var kind = (int)ImageKind.Raw;

        var settings = dbContext.AppSettings.FirstOrDefault();
        if (settings is not null)
        {
            if (settings.UseCalibratedFrames)
            {
                kind = (int)ImageKind.Calibration;
            }
        }

        FormattableString sql =
            $"""
            SELECT DISTINCT 
                strftime('%Y-%m', ObservationTimestampLocal, '-12:00') as Timestamp, 
                SUM(Exposure)/3600 AS TotalExposure
            FROM (
            	SELECT ObservationTimestampLocal, Exposure
            	FROM ImageFiles
            	WHERE
                    Type = {type} AND
                    Kind = {kind} AND
                    ObservationTimestampLocal IS NOT NULL AND
                    ObservationTimestampLocal > {start} AND
                    ObservationTimestampLocal < {end}
            )
            GROUP BY Timestamp
            """;

        var items = dbContext.Database
            .SqlQuery<ExposureGroupingSql>(sql)
            .ToList();

        double[] xValues = items.Select(item => DateTime.ParseExact(item.Timestamp, "yyyy-MM", null).ToOADate()).ToArray();
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

    private record ExposureGroupingSql(string Timestamp, double TotalExposure);

    public record ExposureGroup(DateTime Timestamp, double ExposureHours);
}
