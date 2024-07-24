using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;

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

    private IList<ImageFileViewModel> Search(IReadOnlyList<FilterViewModelBase> filters)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();

        IQueryable<ImageFile> query = dbContext.ImageFiles
            .AsNoTracking()
            .AsQueryable();

        foreach (var filter in filters)
        {
            query = filter.ApplyFilter(dbContext, query);
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
                FileSize = f.FileSize,
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

        StringBuilder filterLogMessage = new();
        filterLogMessage.Append("Search: ");
        filterLogMessage.AppendJoin(", ", message.Filters.Select(f => f.ToString()));
        Log.Information(filterLogMessage.ToString());

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
