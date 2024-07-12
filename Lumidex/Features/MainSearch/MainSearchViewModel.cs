using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch;

public class MainSearchViewModel : ViewModelBase,
    IRecipient<SearchMessage>,
    IRecipient<TagEdited>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public SearchQueryViewModel SearchQueryViewModel { get; }
    public SearchResultsViewModel SearchResultsViewModel { get; }
    public AvaloniaList<ImageFileViewModel> SearchResults { get; } = new();

    public MainSearchViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        SearchQueryViewModel searchQueryViewModel,
        SearchResultsViewModel searchResultsViewModel)
    {
        _dbContextFactory = dbContextFactory;
        SearchQueryViewModel = searchQueryViewModel;
        SearchResultsViewModel = searchResultsViewModel;
    }

    public async void Receive(SearchMessage message)
    {
        bool success = true;

        Log.Information("New search: " +
            "Name = {ObjectName}, " +
            "Library ID = {LibraryId}, " +
            "Image Type = {ImageType}, " +
            "Image Kind = {ImageKind}, " +
            "Exposure Min = {ExposureMin}, " +
            "Exposure Max = {ExposureMax}, " +
            "Filter = {Filter}, " +
            "Date Begin = {DateBegin}, " +
            "Date End = {DateEnd}, " +
            "Tag IDs = {TagIds} ",
        message.Filters.LibraryId,
        message.Filters.Name,
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
            using var dbContext = _dbContextFactory.CreateDbContext();
            var results = await Task.Run(() => dbContext.SearchImageFilesAndProject(message.Filters, ImageFileMapper.ToViewModel));
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

    public void Receive(TagEdited message)
    {
        var tags = SearchResults
            .SelectMany(f => f.Tags)
            .Where(t => t.Id == message.Tag.Id);

        foreach (var tag in tags)
        {
            tag.Name = message.Tag.Name;
            tag.Color = message.Tag.Color;
        }
    }
}
