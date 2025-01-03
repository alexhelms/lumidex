﻿using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase,
    IRecipient<ObjectNameSearchFill>,
    IRecipient<ExitingMessage>
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    [ObservableProperty]
    public partial ObservableCollectionEx<FilterViewModelBase> AvailableFilters { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollectionEx<FilterViewModelBase> ActiveFilters { get; set; } = [];

    public IEnumerable<FilterViewModelBase> AllFilters => ActiveFilters.Concat(AvailableFilters);

    public SearchQueryViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        // Default filters
        ObjectNameFilter nameFilter,
        LibraryFilter libraryFilter,
        ImageTypeFilter imageTypeFilter,
        ImageKindFilter imageKindFilter,
        ExposureFilter exposureFilter,
        FilterFilter filterFilter,
        ObservationBeginUtcFilter observationBeginFilter,
        ObservationEndUtcFilter observationEndFilter,
        TagFilter tagFilter,
        // Advanced filters
        PathFilter pathFilter,
        ObservationBeginLocalFilter observationBeginLocalFilter,
        ObservationEndLocalFilter observationEndLocalFilter,
        CameraNameFilter cameraNameFilter,
        CameraTemperatureSetPointFilter cameraTemperatureSetPointFilter,
        CameraTemperatureFilter cameraTemperatureFilter,
        CameraGainFilter cameraGainFilter,
        CameraOffsetFilter cameraOffsetFilter,
        CameraBinningFilter cameraBinningFilter,
        PixelSizeFilter pixelSizeFilter,
        ReadoutModeFilter readoutModeFilter,
        FocuserNameFilter focuserNameFilter,
        FocuserPositionFilter focuserPositionFilter,
        FocuserTemperatureFilter focuserTemperatureFilter,
        RotatorNameFilter rotatorNameFilter,
        RotatorPositionFilter rotatorPositionFilter,
        FilterWheelNameFilter filterWheelNameFilter,
        TelescopeNameFilter mountNameFilter,
        RightAscensionFilter rightAscensionFilter,
        DeclinationFilter declinationFilter,
        AltitudeFilter altitudeFilter,
        AzimuthFilter azimuthFilter,
        FocalLengthFilter focalLengthFilter,
        AirmassFilter airmassFilter,
        LatitudeFilter latitudeFilter,
        LongitudeFilter longitudeFilter,
        ElevationFilter elevationFilter,
        DewPointFilter dewPointFilter,
        HumidityFilter humidityFilter,
        PressureFilter pressureFilter,
        TemperatureFilter temperatureFilter,
        CoordinateFilter coordinateFilter)
    {
        _dbContextFactory = dbContextFactory;

        ActiveFilters.AddRange([
            nameFilter,
            libraryFilter,
            imageTypeFilter,
            imageKindFilter,
            exposureFilter,
            filterFilter,
            observationBeginFilter,
            observationEndFilter,
            tagFilter,
        ]);

        List<FilterViewModelBase> allFilters = [
            pathFilter,
            observationBeginLocalFilter,
            observationEndLocalFilter,
            cameraNameFilter,
            cameraTemperatureSetPointFilter,
            cameraTemperatureFilter,
            cameraGainFilter,
            cameraOffsetFilter,
            cameraBinningFilter,
            pixelSizeFilter,
            readoutModeFilter,
            focuserNameFilter,
            focuserPositionFilter,
            focuserTemperatureFilter,
            rotatorNameFilter,
            rotatorPositionFilter,
            filterWheelNameFilter,
            mountNameFilter,
            rightAscensionFilter,
            declinationFilter,
            altitudeFilter,
            azimuthFilter,
            focalLengthFilter,
            airmassFilter,
            latitudeFilter,
            longitudeFilter,
            elevationFilter,
            dewPointFilter,
            humidityFilter,
            pressureFilter,
            temperatureFilter,
            coordinateFilter,
        ];

        AvailableFilters.AddRange(allFilters.OrderBy(f => f.DisplayName));
        RestoreFilters();
    }

    private void RestoreFilters()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var settings = dbContext.AppSettings
            .AsNoTracking()
            .Include(x => x.PersistedFilters)
            .FirstOrDefault();

        if (settings is { PersistFiltersOnExit: true })
        {
            // Temp list since we can't alter the enumeration while enumerating AllFilters...
            var filtersToRestore = new List<FilterViewModelBase>();

            // Yes this is slow, but it isn't doing much so it's probably fine.
            foreach (var filter in AllFilters)
            {
                foreach (var item in settings.PersistedFilters)
                {
                    try
                    {
                        if (filter.Restore(item))
                        {
                            filtersToRestore.Add(filter);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error restoring filter {Filter}", filter.DisplayName);
                    }
                }
            }

            foreach (var filter in filtersToRestore)
            {
                AddAdvancedFilter(filter);
            }
        }
    }

    public void PersistFilters()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var settings = dbContext.AppSettings
            .Include(x => x.PersistedFilters)
            .FirstOrDefault();

        if (settings is { PersistFiltersOnExit: true })
        {
            settings.PersistedFilters.Clear();

            foreach (var filter in AllFilters)
            {
                try
                {
                    var data = filter.Persist();
                    if (data is not null)
                    {
                        settings.PersistedFilters.Add(data);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error persisting {Filter}", filter);
                }
            }

            dbContext.SaveChanges();
        }
    }

    [RelayCommand]
    private void AddAdvancedFilter(FilterViewModelBase filter)
    {
        if (AvailableFilters.Remove(filter))
        {
            ActiveFilters.Add(filter);
        }
    }

    [RelayCommand]
    private void RemoveAdvancedFilter(FilterViewModelBase filter)
    {
        if (ActiveFilters.Remove(filter))
        {
            filter.ClearCommand.Execute(null);

            // Insert in the list while maintaining alphabetical order
            var index = AvailableFilters
                .Select(f => f.DisplayName)
                .ToList()
                .BinarySearch(filter.DisplayName);
            if (index < 0)
            {
                AvailableFilters.Insert(~index, filter);
            }
            else
            {
                AvailableFilters.Insert(index, filter);
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var filter in ActiveFilters)
        {
            filter.ClearCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void Search()
    {
        Messenger.Send(new Search
        {
            Filters = ActiveFilters,
        });
    }

    [RelayCommand]
    private void SearchPrev1Day()
    {
        if (ActiveFilters.OfType<ObservationBeginUtcFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-1);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev3Day()
    {
        if (ActiveFilters.OfType<ObservationBeginUtcFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-3);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev7Day()
    {
        if (ActiveFilters.OfType<ObservationBeginUtcFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-7);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev30Day()
    {
        if (ActiveFilters.OfType<ObservationBeginUtcFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-30);
            Search();
        }
    }

    public void Receive(ObjectNameSearchFill message)
    {
        var filter = ActiveFilters.OfType<ObjectNameFilter>().FirstOrDefault();
        if (filter is null)
        {
            filter = AvailableFilters.OfType<ObjectNameFilter>().First();
            AvailableFilters.Remove(filter);
            ActiveFilters.Insert(0, filter);
        }

        filter.Name = message.ObjectName;
        Search();
    }

    public void Receive(ExitingMessage message)
    {
        PersistFilters();
    }
}