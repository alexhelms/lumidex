using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Lumidex.Features.MainSearch;

public partial class MainSearchViewModel : ViewModelBase,
    IRecipient<SearchMessage>,
    IRecipient<TagEdited>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public SearchQueryViewModel SearchQueryViewModel { get; }
    public SearchResultsViewModel SearchResultsViewModel { get; }

    [ObservableProperty] ObservableCollectionEx<ImageFileViewModel> _searchResults = new();

    public MainSearchViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        SearchQueryViewModel searchQueryViewModel,
        SearchResultsViewModel searchResultsViewModel)
    {
        _dbContextFactory = dbContextFactory;
        SearchQueryViewModel = searchQueryViewModel;
        SearchResultsViewModel = searchResultsViewModel;
    }

    private IList<ImageFileViewModel> Search(ImageFileFilters filters)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        IQueryable<ImageFile> query = dbContext.ImageFiles
            .AsNoTracking()
            .AsQueryable();

        if (filters.Name is { Length: > 0 })
        {
            // Create a set that contains the user's filter, any matching alias, and any matching object name
            var aliases = new HashSet<string>([filters.Name], StringComparer.InvariantCultureIgnoreCase);

            aliases.UnionWith(dbContext.ObjectAliases
                .Where(a => EF.Functions.Like(a.Alias, $"%{filters.Name}%"))
                .Select(a => a.ObjectName));

            aliases.UnionWith(dbContext.ImageFiles
                .Where(f => f.ObjectName != null)
                .Select(f => f.ObjectName)
                .Where(objectName => EF.Functions.Like(objectName, $"%{filters.Name}%"))!);

            query = query
                .Where(f => f.ObjectName != null)
                .Where(f => aliases.Contains(f.ObjectName!));
        }

        if (filters.LibraryId.HasValue)
            query = query.Where(f => f.LibraryId == filters.LibraryId);

        if (filters.ImageType is { } imageType)
            query = query.Where(f => f.Type == imageType);

        if (filters.ImageKind is { } imageKind)
            query = query.Where(f => f.Kind == imageKind);

        if (filters.ExposureMin is { } min)
            query = query.Where(f => f.Exposure!.Value >= min.TotalSeconds);

        if (filters.ExposureMax is { } max)
            query = query.Where(f => f.Exposure!.Value <= max.TotalSeconds);

        if (filters.Filter is { Length: > 0 } filter)
            query = query.Where(f => f.FilterName == filter);

        if (filters.DateBegin is { } dateBegin)
            query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);

        if (filters.DateEnd is { } dateEnd)
            query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);

        if (filters.TagIds is not null && filters.TagIds.Any())
        {
            var tagIds = filters.TagIds.ToHashSet();
            query = query.Where(f => f.Tags.Any(tag => tagIds.Contains(tag.Id)));
        }

        var imageFiles = query
            .Select(f => new ImageFileViewModel
            {
                Id = f.Id,
                LibraryName = f.Library.Name,
                Tags = new (f.Tags
                    .Select(tag => new TagViewModel
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        Color = tag.Color,
                    })),
                Path = f.Path,
                Type = f.Type,
                Kind = f.Kind,
                // Camera
                CameraName = f.CameraName,
                Exposure = f.Exposure,
                CameraTemperatureSetPoint = f.CameraTemperatureSetPoint,
                CameraTemperature = f.CameraTemperature,
                CameraGain = f.CameraGain,
                CameraOffset = f.CameraOffset,
                Binning = f.Binning,
                PixelSize = f.PixelSize,
                ReadoutMode = f.ReadoutMode,
                //Focuser
                FocuserName = f.FocuserName,
                FocuserPosition = f.FocuserPosition,
                FocuserTemperature = f.FocuserTemperature,
                // Rotator
                RotatorName = f.RotatorName,
                RotatorPosition = f.RotatorPosition,
                // FilterWheel
                FilterWheelName = f.FilterWheelName,
                FilterName = f.FilterName,
                //Mount
                MountName = f.MountName,
                RightAscension = f.RightAscension,
                Declination = f.Declination,
                Altitude = f.Altitude,
                Azimuth = f.Azimuth,
                // Telescope
                FocalLength = f.FocalLength,
                Airmass = f.Airmass,
                // Target
                ObservationTimestampUtc = f.ObservationTimestampUtc,
                ObservationTimestampLocal = f.ObservationTimestampLocal,
                ObjectName = f.ObjectName,
                // Site
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                Elevation = f.Elevation,
                // Weather
                DewPoint = f.DewPoint,
                Humidity = f.Humidity,
                Pressure = f.Pressure,
                Temperature = f.Temperature,
            })
            .ToList();

        return imageFiles;
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
            var start = Stopwatch.GetTimestamp();
            
            var results = await Task.Run(() => Search(message.Filters));
            
            var elapsed = Stopwatch.GetElapsedTime(start);
            Log.Information($"Query completed in {elapsed.TotalSeconds:F3} seconds");

            SearchResults = new(results);

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
