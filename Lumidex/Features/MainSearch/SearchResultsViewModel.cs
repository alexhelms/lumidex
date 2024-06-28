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
    [ObservableProperty] AvaloniaList<ImageFile> _searchResults = new();

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
            });

            var query = _dbContext.ImageFiles.AsNoTracking();

            if (message.ObjectName is { })
                query = query.Where(f => EF.Functions.Like(f.ObjectName, $"%{message.ObjectName}%"));

            if (message.ImageType is { } imageType)
                query = query.Where(f => f.Type == imageType);

            if (message.ImageKind is { } imageKind )
                query = query.Where(f => f.Kind == imageKind);

            if (message.ExposureMin is { } min)
                query = query.Where(f => f.Exposure!.Value >= min.TotalSeconds);

            if (message.ExposureMax is { } max)
                query = query.Where(f => f.Exposure!.Value <= max.TotalSeconds);

            if (message.Filter is { } filter)
            {
                query = query.Where(f => f.FilterName == filter);
            }

            if (message.DateBegin is { } dateBegin)
            {
                query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);
            }

            if (message.DateEnd is { } dateEnd)
            {
                query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);
            }

            var results = await query.ToListAsync(_searchCts.Token);
            await Dispatcher.UIThread.InvokeAsync(() => SearchResults.AddRange(results));
        }
        catch (OperationCanceledException) { }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSearching = false);
        }
    }
}
    