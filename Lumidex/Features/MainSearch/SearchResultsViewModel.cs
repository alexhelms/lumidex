using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch;

public partial class SearchResultsViewModel : ViewModelBase,
    IRecipient<QueryMessage>
{
    private readonly LumidexDbContext _dbContext;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty] bool _isSearching;
    [ObservableProperty] string? _totalIntegration;
    [ObservableProperty] string? _typeAggregate;
    [ObservableProperty] int _lightCount;
    [ObservableProperty] int _flatCount;
    [ObservableProperty] int _darkCount;
    [ObservableProperty] int _biasCount;
    [ObservableProperty] int _unknownCount;
    [ObservableProperty] int _distintObjectNameCount;

    public AvaloniaList<ImageFile> SearchResults { get; } = new();
    public AvaloniaList<IntegrationStatistic> IntegrationStats { get; } = new();

    public SearchResultsViewModel(
        LumidexDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    ~SearchResultsViewModel()
    {
        _searchCts?.Dispose();
    }

    public async void Receive(QueryMessage message)
    {
        await Search(message);
    }

    private async Task Search(QueryMessage message)
    {
        // Cancel an existing search and wait for it to complete
        if (IsSearching)
        {
            _searchCts?.Cancel();
            while (IsSearching)
            {
                await Task.Delay(100);
            }
        }

        try
        {
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearching = true;
                SearchResults.Clear();
                IntegrationStats.Clear();
                TotalIntegration = null;
                TypeAggregate = null;
                LightCount = 0;
                FlatCount = 0;
                DarkCount = 0;
                BiasCount = 0;
                UnknownCount = 0;
            });

            var query = _dbContext.ImageFiles.AsNoTracking();

            if (message.Library is not null)
                query = query.Where(f => f.LibraryId == message.Library.Id);

            if (message.ObjectName is { Length: >0 })
                query = query.Where(f => EF.Functions.Like(f.ObjectName, $"%{message.ObjectName}%"));

            if (message.ImageType is { } imageType)
                query = query.Where(f => f.Type == imageType);

            if (message.ImageKind is { } imageKind )
                query = query.Where(f => f.Kind == imageKind);

            if (message.ExposureMin is { } min)
                query = query.Where(f => f.Exposure!.Value >= min.TotalSeconds);

            if (message.ExposureMax is { } max)
                query = query.Where(f => f.Exposure!.Value <= max.TotalSeconds);

            if (message.Filter is { Length: > 0 } filter)
                query = query.Where(f => f.FilterName == filter);

            if (message.DateBegin is { } dateBegin)
                query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);

            if (message.DateEnd is { } dateEnd)
                query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);

            var results = await query.ToListAsync(_searchCts.Token);

            SearchResults.AddRange(results);
            IntegrationStats.AddRange(ComputeIntegrationStatistics(results));
            
            double totalIntegrationSum = IntegrationStats.Sum(x => x.TotalIntegration.TotalHours);
            TotalIntegration = totalIntegrationSum < 1
                ? totalIntegrationSum.ToString("F2")
                : totalIntegrationSum.ToString("F1");

            foreach (var item in SearchResults)
            {
                if (item.Type == ImageType.Light) LightCount++;
                else if (item.Type == ImageType.Flat) FlatCount++;
                else if (item.Type == ImageType.Dark) DarkCount++;
                else if (item.Type == ImageType.Bias) BiasCount++;
                else UnknownCount++;
            }
            TypeAggregate = string.Join('/', LightCount, FlatCount, DarkCount, BiasCount, UnknownCount);
            DistintObjectNameCount = results
                .Select(x => x.ObjectName)
                .Where(x => x is not null)
                .Distinct()
                .Count();
        }
        catch (OperationCanceledException) { }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSearching = false);
        }
    }

    private static IEnumerable<IntegrationStatistic> ComputeIntegrationStatistics(List<ImageFile> images)
    {
        var results = images
            .Where(img => img.Type == ImageType.Light)
            .GroupBy(img => img.FilterName)
            .Select(grp => new IntegrationStatistic
            {
                Filter = grp.Key ?? "None",
                Count = grp.Count(),
                TotalIntegration = TimeSpan.FromSeconds(grp
                    .Sum(img => img.Exposure.GetValueOrDefault()))

            });

        return new AvaloniaList<IntegrationStatistic>(results);
    }
}

public class IntegrationStatistic
{
    public string? Filter { get; init; }
    public int Count { get; init; }
    public TimeSpan TotalIntegration { get; init; }

    public string CountDisplay => Count.ToString();
    public string TotalIntegrationDisplay
    {
        get
        {
            if (TotalIntegration < TimeSpan.FromHours(1))
            {
                return TotalIntegration.TotalHours.ToString("F2");
            }

            return TotalIntegration.TotalHours.ToString("F1");
        }
    }
}