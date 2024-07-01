using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase
{
    [ObservableProperty] string? _objectName;
    [ObservableProperty] ImageKind? _selectedImageKind;
    [ObservableProperty] ImageType? _selectedImageType;
    [ObservableProperty] decimal? _exposureMin;
    [ObservableProperty] decimal? _exposureMax;
    [ObservableProperty] string? _selectedFilter;
    [ObservableProperty] DateTime? _selectedDateBegin;
    [ObservableProperty] DateTime? _selectedDateEnd;

    public List<ImageKind> ImageKinds { get; } = Enum.GetValues<ImageKind>().OrderBy(x => x.ToString()).ToList();
    public List<ImageType> ImageTypes { get; } = Enum.GetValues<ImageType>().OrderBy(x => x.ToString()).ToList();

    [RelayCommand]
    private void ClearObjectName() => ObjectName = null;

    [RelayCommand]
    private void ClearSelectedImageType() => SelectedImageType = null;

    [RelayCommand]
    private void ClearSelectedImageKind() => SelectedImageKind = null;

    [RelayCommand]
    private void ClearExposure()
    {
        ExposureMin = null;
        ExposureMax = null;
    }

    [RelayCommand]
    private void ClearSelectedFilter() => SelectedFilter = null;

    [RelayCommand]
    private void ClearDateBegin() => SelectedDateBegin = null;

    [RelayCommand]
    private void ClearDateEnd() => SelectedDateEnd = null;

    [RelayCommand]
    private void Clear()
    {
        ClearObjectName();
        ClearSelectedImageType();
        ClearSelectedImageKind();
        ClearExposure();
        ClearSelectedFilter();
        ClearDateBegin();
        ClearDateEnd();
    }

    [RelayCommand]
    private void Search()
    {
        Messenger.Send(new QueryMessage
        {
            ObjectName = ObjectName,
            ImageType = SelectedImageType,
            ImageKind = SelectedImageKind,
            ExposureMin = ExposureMin.HasValue ? TimeSpan.FromSeconds((double)ExposureMin.Value) : null,
            ExposureMax = ExposureMax.HasValue ? TimeSpan.FromSeconds((double)ExposureMax.Value) : null,
            Filter = SelectedFilter,
            DateBegin = SelectedDateBegin,
            DateEnd = SelectedDateEnd,
        });
    }
}
