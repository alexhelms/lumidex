using AutoMapper;
using AutoMapper.QueryableExtensions;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase
{
    private readonly LumidexDbContext _dbContext;
    private readonly IMapper _mapper;

    private LibraryViewModel? _prevLibrary;
    private List<int> _prevSelectedTagIds = new();

    [ObservableProperty] LibraryViewModel? _library;
    [ObservableProperty] string? _objectName;
    [ObservableProperty] ImageKind? _selectedImageKind;
    [ObservableProperty] ImageType? _selectedImageType;
    [ObservableProperty] decimal? _exposureMin;
    [ObservableProperty] decimal? _exposureMax;
    [ObservableProperty] string? _selectedFilter;
    [ObservableProperty] DateTime? _selectedDateBegin;
    [ObservableProperty] DateTime? _selectedDateEnd;
    [ObservableProperty] AvaloniaList<LibraryViewModel> _libraries = new();
    [ObservableProperty] AvaloniaList<TagViewModel> _tags = new();
    [ObservableProperty] AvaloniaList<TagViewModel> _selectedTags = new();
    [ObservableProperty] AvaloniaList<TagViewModel> _queryTags = new();

    public List<ImageKind> ImageKinds { get; } = Enum.GetValues<ImageKind>().OrderBy(x => x.ToString()).ToList();
    public List<ImageType> ImageTypes { get; } = Enum.GetValues<ImageType>().OrderBy(x => x.ToString()).ToList();

    public SearchQueryViewModel(
        LumidexDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    protected override async void OnActivated()
    {
        base.OnActivated();

        var libraries = await _dbContext.Libraries
            .AsNoTracking()
            .OrderBy(library => library.Name)
            .ProjectTo<LibraryViewModel>(_mapper.ConfigurationProvider)
            .ToListAsync();

        var tags = await _dbContext.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.TaggedImages.Count)
            .ThenBy(tag => tag.Name)
            .ProjectTo<TagViewModel>(_mapper.ConfigurationProvider)
            .ToListAsync();

        Libraries = new(libraries);
        Tags = new(tags);

        // Select the previously selected library
        if (_prevLibrary is not null)
        {
            Library = Libraries.FirstOrDefault(x => x.Id == _prevLibrary.Id);
        }
        
        if (_prevSelectedTagIds.Count > 0)
        {
            SelectedTags.AddRange(Tags.Where(tag => _prevSelectedTagIds.Contains(tag.Id)));
        }
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        // When the view is deactivated, the binding clears Library so store the previously
        // selected library in another variable so it can be restored on activation.
        _prevLibrary = Library;

        _prevSelectedTagIds.Clear();
        _prevSelectedTagIds.AddRange(SelectedTags.Select(x => x.Id));
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
    private void ClearTags() => QueryTags.Clear();

    [RelayCommand]
    private void Clear()
    {
        ClearLibrary();
        ClearObjectName();
        ClearSelectedImageType();
        ClearSelectedImageKind();
        ClearExposure();
        ClearSelectedFilter();
        ClearDateBegin();
        ClearDateEnd();
        ClearTags();
    }

    [RelayCommand]
    private void Search()
    {
        Messenger.Send(new QueryMessage
        {
            Library = Library,
            ObjectName = ObjectName,
            ImageType = SelectedImageType,
            ImageKind = SelectedImageKind,
            ExposureMin = ExposureMin.HasValue ? TimeSpan.FromSeconds((double)ExposureMin.Value) : null,
            ExposureMax = ExposureMax.HasValue ? TimeSpan.FromSeconds((double)ExposureMax.Value) : null,
            Filter = SelectedFilter,
            DateBegin = SelectedDateBegin,
            DateEnd = SelectedDateEnd,
            TagIds = QueryTags.Select(t => t.Id).ToArray(),
        });
    }
}