using Lumidex.Core.Data;
using Lumidex.Features.Library.Messages;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;

namespace Lumidex.Features.MainSearch;

public partial class SearchQueryViewModel : ViewModelBase,
    IRecipient<LibraryCreated>,
    IRecipient<LibraryDeleted>,
    IRecipient<LibraryEdited>,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>
{
    [ObservableProperty] LibraryViewModel? _library;
    [ObservableProperty] string? _objectName;
    [ObservableProperty] ImageKind? _selectedImageKind;
    [ObservableProperty] ImageType? _selectedImageType;
    [ObservableProperty] decimal? _exposureMin;
    [ObservableProperty] decimal? _exposureMax;
    [ObservableProperty] string? _selectedFilter;
    [ObservableProperty] DateTime? _selectedDateBegin;
    [ObservableProperty] DateTime? _selectedDateEnd;
    [ObservableProperty] ObservableCollectionEx<LibraryViewModel> _libraries = new ();
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _tags = new ();
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _selectedTags = new ();
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _queryTags = new ();

    public List<ImageKind> ImageKinds { get; } = Enum.GetValues<ImageKind>().OrderBy(x => x.ToString()).ToList();
    public List<ImageType> ImageTypes { get; } = Enum.GetValues<ImageType>().OrderBy(x => x.ToString()).ToList();

    public void Receive(LibraryCreated message)
    {
        if (!Libraries.Contains(message.Library))
            Libraries.Add(message.Library);
    }

    public void Receive(LibraryDeleted message)
    {
        Libraries.Remove(message.Library);
    }

    public void Receive(LibraryEdited message)
    {
        if (Libraries.FirstOrDefault(library => library == message.Library) is { } existingLibrary)
        {
            // Retain a reference to the selected library, if one exists. Removing it from the list deselects!
            LibraryViewModel? selectedLibrary = Library;

            var index = Libraries.IndexOf(existingLibrary);
            Libraries.RemoveAt(index);
            Libraries.Insert(index, message.Library);

            // Retain user selection
            if (selectedLibrary is not null && selectedLibrary == message.Library)
                Library = message.Library;
        }
    }

    public void Receive(TagCreated message)
    {
        if (!Tags.Contains(message.Tag))
            Tags.Add(message.Tag);
    }

    public void Receive(TagDeleted message)
    {
        Tags.Remove(message.Tag);
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
        Messenger.Send(new SearchMessage
        {
            Filters = new ImageFileFilters
            {
                LibraryId = Library?.Id,
                Name = ObjectName,
                ImageType = SelectedImageType,
                ImageKind = SelectedImageKind,
                ExposureMin = ExposureMin.HasValue ? TimeSpan.FromSeconds((double)ExposureMin.Value) : null,
                ExposureMax = ExposureMax.HasValue ? TimeSpan.FromSeconds((double)ExposureMax.Value) : null,
                Filter = SelectedFilter,
                DateBegin = SelectedDateBegin,
                DateEnd = SelectedDateEnd,
                TagIds = QueryTags.Select(t => t.Id).ToArray(),
            }
        });
    }

    [RelayCommand]
    private void SearchPrev1Day()
    {
        SelectedDateBegin = DateTime.UtcNow.AddDays(-1);
        Search();
    }

    [RelayCommand]
    private void SearchPrev3Day()
    {
        SelectedDateBegin = DateTime.UtcNow.AddDays(-3);
        Search();
    }

    [RelayCommand]
    private void SearchPrev7Day()
    {
        SelectedDateBegin = DateTime.UtcNow.AddDays(-7);
        Search();
    }

    [RelayCommand]
    private void SearchPrev30Day()
    {
        SelectedDateBegin = DateTime.UtcNow.AddDays(-30);
        Search();
    }
}