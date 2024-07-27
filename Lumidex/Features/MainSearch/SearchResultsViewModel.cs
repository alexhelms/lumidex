using Avalonia.Threading;
using Humanizer;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Actions;
using Lumidex.Features.MainSearch.Editing;
using Lumidex.Features.MainSearch.Editing.Messages;
using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Lumidex.Services;
using System.IO.Abstractions;

namespace Lumidex.Features.MainSearch;

public partial class SearchResultsViewModel : ViewModelBase,
    IRecipient<SearchStarting>,
    IRecipient<SearchResultsReady>,
    IRecipient<SearchComplete>,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>,
    IRecipient<ImageFilesEdited>
{
    private readonly IFileSystem _fileSystem;
    private readonly SystemService _systemService;
    private readonly DialogService _dialogService;
    private readonly Func<EditItemsViewModel> _editItemsViewModelFactory;

    [ObservableProperty] bool _isSearching;
    [ObservableProperty] string? _totalIntegration;
    [ObservableProperty] string? _typeAggregate;
    [ObservableProperty] int _lightCount;
    [ObservableProperty] int _flatCount;
    [ObservableProperty] int _darkCount;
    [ObservableProperty] int _biasCount;
    [ObservableProperty] int _unknownCount;
    [ObservableProperty] string? _totalFileSize;
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _allTags = new();
    [ObservableProperty] ObservableCollectionEx<ImageFileViewModel> _searchResults = new();
    [ObservableProperty] ObservableCollectionEx<FilterViewModelBase> _activeFilters = new();
    [ObservableProperty] ObservableCollectionEx<ImageFileViewModel> _selectedSearchResults = new();
    [ObservableProperty] ObservableCollectionEx<IntegrationStatistic> _integrationStats = new();
    [ObservableProperty] ObservableCollectionEx<string> _distinctObjectNames = new();
    [ObservableProperty] int _selectedSearchResultsCount;

    public ActionsContainerViewModel ActionsViewModel { get; }
    
    public SearchResultsViewModel(
        IFileSystem fileSystem,
        SystemService systemService,
        DialogService dialogService,
        ActionsContainerViewModel actionsViewModel,
        Func<EditItemsViewModel> editItemsViewModelFactory)
    {
        _fileSystem = fileSystem;
        _systemService = systemService;
        _dialogService = dialogService;

        ActionsViewModel = actionsViewModel;
        _editItemsViewModelFactory = editItemsViewModelFactory;
    }

    private IEnumerable<string> GetDistinctObjectNames(IEnumerable<ImageFileViewModel> items)
    {
        return items
            .Where(x => x.ObjectName != null)
            .Select(x => x.ObjectName)
            .Distinct()
            .Order()!;
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();

        AllTags = new(Messenger.Send(new GetTags()).Response);
    }

    public void Receive(SearchStarting message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            IsSearching = true;
            ActiveFilters = new(message.Filters);
            SearchResults.Clear();
            IntegrationStats.Clear();
            DistinctObjectNames.Clear();
            TotalIntegration = null;
            TypeAggregate = null;
            LightCount = 0;
            FlatCount = 0;
            DarkCount = 0;
            BiasCount = 0;
            UnknownCount = 0;
            TotalFileSize = null;
        });
    }

    public void Receive(SearchComplete message)
    {
        Dispatcher.UIThread.Invoke(() => IsSearching = false);
    }

    public void Receive(SearchResultsReady message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var stats = ComputeIntegrationStatistics(message.SearchResults);
            IntegrationStats.AddRange(stats);

            double totalIntegrationSum = IntegrationStats.Sum(x => x.TotalIntegration.TotalHours);
            TotalIntegration = totalIntegrationSum < 1
                ? totalIntegrationSum.ToString("F2")
                : totalIntegrationSum.ToString("F1");

            foreach (var item in message.SearchResults)
            {
                if (item.Type == ImageType.Light) LightCount++;
                else if (item.Type == ImageType.Flat) FlatCount++;
                else if (item.Type == ImageType.Dark) DarkCount++;
                else if (item.Type == ImageType.Bias) BiasCount++;
                else UnknownCount++;
            }
            TypeAggregate = string.Join('/', LightCount, FlatCount, DarkCount, BiasCount, UnknownCount);
            DistinctObjectNames = new(GetDistinctObjectNames(message.SearchResults));
            TotalFileSize = message.SearchResults.Sum(f => f.FileSize).Bytes().ToString("0.00 GB");

            SearchResults = message.SearchResults;
        });

        static IEnumerable<IntegrationStatistic> ComputeIntegrationStatistics(IEnumerable<ImageFileViewModel> images)
        {
            var results = images
                .Where(img => img.Type == ImageType.Light)
                .GroupBy(img => img.FilterName)
                .Select(grp => new IntegrationStatistic
                {
                    Filter = grp.Key ?? "None",
                    Count = grp.Count(),
                    TotalIntegration = TimeSpan.FromSeconds(grp
                        .Sum(img => img.Exposure.GetValueOrDefault()))

                });

            return new ObservableCollectionEx<IntegrationStatistic>(results);
        }
    }

    public void Receive(TagCreated message)
    {
        if (!AllTags.Contains(message.Tag))
            AllTags.Add(message.Tag);
    }

    public void Receive(TagDeleted message)
    {
        AllTags.Remove(message.Tag);
    }

    public void Receive(ImageFilesEdited message)
    {
        DistinctObjectNames = new(GetDistinctObjectNames(SearchResults));
    }

    [RelayCommand]
    private async Task ShowEditDialog()
    {
        var vm = _editItemsViewModelFactory();
        vm.SelectedItems = SelectedSearchResults;
        _ = await _dialogService.ShowDialog(vm, onOpen: (o, e) =>
        {
            vm.CloseDialog = () => e.Session.Close();
        });
    }

    [RelayCommand]
    private void AddTag(TagViewModel tag)
    {
        Messenger.Send(new AddTag
        {
            Tag = tag,
            ImageFiles = SelectedSearchResults,
        });
    }

    [RelayCommand]
    public void RemoveTag(TagViewModel tag)
    {
        Messenger.Send(new RemoveTag
        {
            Tag = tag,
            ImageFiles = SelectedSearchResults,
        });
    }

    [RelayCommand]
    private void RemoveAllTagsFromSelection()
    {
        Messenger.Send(new ClearTags
        {
            ImageFiles = SelectedSearchResults,
        });
    }

    [RelayCommand]
    private async Task OpenInExplorer(ImageFileViewModel imageFile)
    {
        IFileInfo fileInfo = _fileSystem.FileInfo.New(imageFile.Path);
        if (fileInfo.Exists)
        {
            await _systemService.StartProcess("explorer.exe", $"/select,\"{fileInfo.FullName}\"");
        }
    }

    [RelayCommand]
    private async Task OpenInPixInsight(ImageFileViewModel imageFile)
    {
        IFileInfo fileInfo = _fileSystem.FileInfo.New(imageFile.Path);
        if (fileInfo.Exists)
        {
            // TODO: Application setting to specify PixInsight.exe path?
            await _systemService.StartProcess(@"C:\Program Files\PixInsight\bin\PixInsight.exe", fileInfo.FullName);
        }
    }
}

public class IntegrationStatistic
{
    public string? Filter { get; init; }
    public int Count { get; init; }
    public TimeSpan TotalIntegration { get; init; }

    public string CountDisplay => Count.ToString();
    public string TotalIntegrationDisplay
    {
        get
        {
            if (TotalIntegration < TimeSpan.FromHours(1))
            {
                return TotalIntegration.TotalHours.ToString("F2");
            }

            return TotalIntegration.TotalHours.ToString("F1");
        }
    }
}