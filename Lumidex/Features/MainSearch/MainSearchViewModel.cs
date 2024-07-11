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

        Log.Information("New search: " +
            "Library ID = {LibraryId}, " +
            "Object Name = {ObjectName}, " +
            "Image Type = {ImageType}, " +
            "Image Kind = {ImageKind}, " +
            "Exposure Min = {ExposureMin}, " +
            "Exposure Max = {ExposureMax}, " +
            "Filter = {Filter}, " +
            "Date Begin = {DateBegin}, " +
            "Date End = {DateEnd}, " +
            "Tag IDs = {TagIds} ",
        message.Filters.LibraryId,
        message.Filters.ObjectName,
        message.Filters.ImageType,
        message.Filters.ImageKind,
        message.Filters.ExposureMin,
        message.Filters.ExposureMax,
        message.Filters.Filter,
        message.Filters.DateBegin,
        message.Filters.DateEnd,
        message.Filters.TagIds);

        Messenger.Send(new SearchStarting());

        try
        {
            var results = await Task.Run(() => _dbContext.SearchImageFilesAndProject(message.Filters, ImageFileMapper.ToViewModel));
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
