using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch;

public class MainSearchViewModel : ViewModelBase,
    IRecipient<SearchMessage>

{
    private readonly LumidexDbContext _dbContext;
    private readonly Lazy<SearchQueryViewModel> _searchQuery;
    private readonly Lazy<SearchResultsViewModel> _searchResults;

    public SearchQueryViewModel SearchQueryViewModel => _searchQuery.Value;
    public SearchResultsViewModel SearchResultsViewModel => _searchResults.Value;
    public AvaloniaList<ImageFileViewModel> SearchResults { get; } = new();

    public MainSearchViewModel(
        LumidexDbContext dbContext,
        Lazy<SearchQueryViewModel> searchQuery,
        Lazy<SearchResultsViewModel> searchResults)
    {
        _dbContext = dbContext;
        _searchQuery = searchQuery;
        _searchResults = searchResults;
    }

    public async void Receive(SearchMessage message)
    {
        bool success = true;

        Messenger.Send(new SearchStarting());

        try
        {
            var results = await Task.Run(() => _dbContext.SearchImageFilesAndProject<ImageFileViewModel>(message.Filters));
            SearchResults.Clear();
            SearchResults.AddRange(results);

            Messenger.Send(new SearchResultsReady { SearchResults = SearchResults });
        }
        catch
        {
            success = false;
        }
        finally
        {
            Messenger.Send(new SearchComplete { IsSuccess = success });
        }
    }
}
