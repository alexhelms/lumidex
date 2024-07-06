using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase
{
    private readonly LumidexDbContext _dbContext;

    private Core.Data.Library? _prevLibrary;

    [ObservableProperty] Core.Data.Library? _library;
    [ObservableProperty] string? _objectName;
    [ObservableProperty] ImageKind? _selectedImageKind;
    [ObservableProperty] ImageType? _selectedImageType;
    [ObservableProperty] decimal? _exposureMin;
    [ObservableProperty] decimal? _exposureMax;
    [ObservableProperty] string? _selectedFilter;
    [ObservableProperty] DateTime? _selectedDateBegin;
    [ObservableProperty] DateTime? _selectedDateEnd;

    public AvaloniaList<Core.Data.Library> Libraries { get; set; } = new();
    public List<ImageKind> ImageKinds { get; } = Enum.GetValues<ImageKind>().OrderBy(x => x.ToString()).ToList();
    public List<ImageType> ImageTypes { get; } = Enum.GetValues<ImageType>().OrderBy(x => x.ToString()).ToList();

    public SearchQueryViewModel(LumidexDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async void OnActivated()
    {
        base.OnActivated();

        var libraries = await _dbContext.Libraries
            .AsNoTracking()
            .OrderBy(library => library.Name)
            .ToListAsync();

        Libraries.Clear();
        Libraries.AddRange(libraries);

        // Select the previously selected library
        if (_prevLibrary is not null)
        {
            Library = Libraries.FirstOrDefault(x => x.Id == _prevLibrary.Id);
        }
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        // When the view is deactivated, the binding clears Library so store the previously
        // selected library in another variable so it can be restored on activation.
        _prevLibrary = Library;
    }

    [RelayCommand]
    private void ClearLibrary() => Library = null;

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
