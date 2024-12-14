using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text;

namespace Lumidex.Features.MainSearch;

public partial class MainSearchViewModel : ViewModelBase,
    IRecipient<Search>,
    IRecipient<RemoveImageFiles>,
    IRecipient<TagEdited>,
    IRecipient<ImageFilesRemoved>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public SearchQueryViewModel SearchQueryViewModel { get; }
    public SearchResultsViewModel SearchResultsViewModel { get; }

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> SearchResults { get; set; } = new();

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
                RightAscension = f.RightAscension,
                Declination = f.Declination,
                Altitude = f.Altitude,
                Azimuth = f.Azimuth,
                // Telescope
                TelescopeName = f.TelescopeName,
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

    public async void Receive(Search message)
    {
        bool success = true;

        StringBuilder filterLogMessage = new();
        filterLogMessage.Append("Search: ");
        filterLogMessage.AppendJoin(", ", message.Filters.Select(f => f.ToString()));
        Log.Information(filterLogMessage.ToString());

        Messenger.Send(new SearchStarting
        {
            Filters = message.Filters,
        });

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

    public void Receive(RemoveImageFiles message)
    {
        if (!message.ImageFiles.Any()) return;

        var idLookup = message.ImageFiles.Select(f => f.Id).ToHashSet();
        try
        {
            var dbContext = _dbContextFactory.CreateDbContext();
            int count = dbContext.ImageFiles
                .Where(f => idLookup.Contains(f.Id))
                .ExecuteDelete();
            if (count > 0)
            {
                Messenger.Send(new ImageFilesRemoved
                {
                    // Send a copy of the list to avoid enumerable exceptions when items are removed
                    ImageFiles = message.ImageFiles.ToList(),
                });
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error removing image files");
        }
        
    }

    public void Receive(TagEdited message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var tags = SearchResults
            .SelectMany(f => f.Tags)
            .Where(t => t.Id == message.Tag.Id);

            foreach (var tag in tags)
            {
                tag.Name = message.Tag.Name;
                tag.Color = message.Tag.Color;
            }
        });
    }

    public void Receive(ImageFilesRemoved message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var imageFile in message.ImageFiles)
            {
                SearchResults.Remove(imageFile);
            }
        });
    }
}
