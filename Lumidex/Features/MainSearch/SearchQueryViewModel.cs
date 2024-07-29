using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase
{
    [ObservableProperty] ObservableCollectionEx<FilterViewModelBase> _allFilters = new();
    [ObservableProperty] ObservableCollectionEx<FilterViewModelBase> _activeFilters = new();

    public SearchQueryViewModel(
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
        TemperatureFilter temperatureFilter)
    {
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
        ];
        AllFilters.AddRange(allFilters.OrderBy(f => f.DisplayName));
    }

    [RelayCommand]
    private void AddAdvancedFilter(FilterViewModelBase filter)
    {
        if (AllFilters.Remove(filter))
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
            var index = AllFilters
                .Select(f => f.DisplayName)
                .ToList()
                .BinarySearch(filter.DisplayName);
            if (index < 0)
            {
                AllFilters.Insert(~index, filter);
            }
            else
            {
                AllFilters.Insert(index, filter);
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
}