using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase
{
    [ObservableProperty] ObservableCollectionEx<FilterViewModelBase> _activeFilters = new();

    public SearchQueryViewModel(
        NameFilter nameFilter,
        LibraryFilter libraryFilter,
        ImageTypeFilter imageTypeFilter,
        ImageKindFilter imageKindFilter,
        ExposureFilter exposureFilter,
        FilterFilter filterFilter,
        ObservationBeginFilter observationBeginFilter,
        ObservationEndFilter observationEndFilter,
        TagFilter tagFilter)
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
        Messenger.Send(new SearchMessage
        {
            Filters = ActiveFilters,
        });
    }

    [RelayCommand]
    private void SearchPrev1Day()
    {
        if (ActiveFilters.OfType<ObservationBeginFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-1);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev3Day()
    {
        if (ActiveFilters.OfType<ObservationBeginFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-3);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev7Day()
    {
        if (ActiveFilters.OfType<ObservationBeginFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-7);
            Search();
        }
    }

    [RelayCommand]
    private void SearchPrev30Day()
    {
        if (ActiveFilters.OfType<ObservationBeginFilter>().FirstOrDefault() is { } filter)
        {
            filter.DateBegin = DateTime.UtcNow.AddDays(-30);
            Search();
        }
    }
}