namespace Lumidex.Features.MainSearch;

public class MainSearchViewModel : ViewModelBase
{
    private readonly Lazy<SearchQueryViewModel> _searchQuery;
    private readonly Lazy<SearchResultsViewModel> _searchResults;

    public SearchQueryViewModel SearchQuery => _searchQuery.Value;
    public SearchResultsViewModel SearchResults => _searchResults.Value;

    public MainSearchViewModel(
        Lazy<SearchQueryViewModel> searchQuery,
        Lazy<SearchResultsViewModel> searchResults)
    {
        _searchQuery = searchQuery;
        _searchResults = searchResults;
    }
}
