using Avalonia.Threading;
using Humanizer;
using Lumidex.Core.Data;
using Lumidex.Features.AstrobinExport;
using Lumidex.Features.FileExport;
using Lumidex.Features.MainSearch.Actions;
using Lumidex.Features.MainSearch.Editing;
using Lumidex.Features.MainSearch.Editing.Messages;
using Lumidex.Features.MainSearch.Filters;
using Lumidex.Features.MainSearch.Messages;
using Lumidex.Features.Tags.Messages;
using Lumidex.Services;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;

namespace Lumidex.Features.MainSearch;

public partial class SearchResultsViewModel : ViewModelBase,
    IRecipient<SearchStarting>,
    IRecipient<SearchResultsReady>,
    IRecipient<SearchComplete>,
    IRecipient<TagCreated>,
    IRecipient<TagDeleted>,
    IRecipient<ImageFilesEdited>,
    IRecipient<ImageFilesRemoved>
{
    private readonly IFileSystem _fileSystem;
    private readonly SystemService _systemService;
    private readonly DialogService _dialogService;
    private readonly Func<EditItemsViewModel> _editItemsViewModelFactory;
    private readonly Func<AstrobinExportViewModel> _astrobinExportViewModelFactory;
    private readonly Func<FileExportViewModel> _fileExportViewModelFactory;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? TotalIntegration { get; set; }

    [ObservableProperty]
    public partial string? TypeAggregate { get; set; }

    [ObservableProperty]
    public partial int LightCount { get; set; }

    [ObservableProperty]
    public partial int FlatCount { get; set; }

    [ObservableProperty]
    public partial int DarkCount { get; set; }

    [ObservableProperty]
    public partial int BiasCount { get; set; }

    [ObservableProperty]
    public partial int UnknownCount { get; set; }

    [ObservableProperty]
    public partial string? TotalFileSize { get; set; }

    [ObservableProperty]
    public partial ObservableCollectionEx<TagViewModel> AllTags { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> SearchResults { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<FilterViewModelBase> ActiveFilters { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> SelectedSearchResults { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollectionEx<IntegrationStatistic> IntegrationStats { get; set; } = new();

    [ObservableProperty]
    public partial DataGridCollectionView? StatsView { get; set; }

    [ObservableProperty]
    public partial ObservableCollectionEx<string> DistinctObjectNames { get; set; } = new();

    [ObservableProperty]
    public partial int SelectedSearchResultsCount { get; set; }
    public ActionsContainerViewModel ActionsViewModel { get; }
    
    public SearchResultsViewModel(
        IFileSystem fileSystem,
        SystemService systemService,
        DialogService dialogService,
        ActionsContainerViewModel actionsViewModel,
        Func<EditItemsViewModel> editItemsViewModelFactory,
        Func<AstrobinExportViewModel> astrobinExportViewModelFactory,
        Func<FileExportViewModel> fileExportViewModelFactory)
    {
        _fileSystem = fileSystem;
        _systemService = systemService;
        _dialogService = dialogService;

        ActionsViewModel = actionsViewModel;
        _editItemsViewModelFactory = editItemsViewModelFactory;
        _astrobinExportViewModelFactory = astrobinExportViewModelFactory;
        _fileExportViewModelFactory = fileExportViewModelFactory;
    }

    private IEnumerable<string> GetDistinctObjectNames(IEnumerable<ImageFileViewModel> items)
    {
        return items
            .Where(x => x.Type == ImageType.Light)
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
            IsBusy = true;
            ActiveFilters = new(message.Filters);
            SearchResults.Clear();
            IntegrationStats.Clear();
            StatsView = null;
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
        Dispatcher.UIThread.Invoke(() => IsBusy = false);
    }

    public void Receive(SearchResultsReady message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var stats = ComputeIntegrationStatistics(message.SearchResults);
            IntegrationStats.AddRange(stats);
            StatsView = new DataGridCollectionView(IntegrationStats);
            StatsView.SortDescriptions.Add(DataGridSortDescription.FromPath(
                nameof(IntegrationStatistic.TotalIntegration),
                System.ComponentModel.ListSortDirection.Descending,
                Comparer<TimeSpan>.Default));

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
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!AllTags.Contains(message.Tag))
                AllTags.Add(message.Tag);
        });
    }

    public void Receive(TagDeleted message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            AllTags.Remove(message.Tag);
        });
    }

    public void Receive(ImageFilesEdited message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            DistinctObjectNames = new(GetDistinctObjectNames(SearchResults));
        });
    }

    public void Receive(ImageFilesRemoved message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            foreach (var imageFile in message.ImageFiles)
            {
                SearchResults.Remove(imageFile);
                SelectedSearchResults.Remove(imageFile);
            }
        });
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
    private async Task ShowAstrobinExportDialog()
    {
        var vm = _astrobinExportViewModelFactory();
        vm.SelectedItems = SelectedSearchResults;
        _ = await _dialogService.ShowDialog(vm, onOpen: (o, e) =>
        {
            vm.CloseDialog = () => e.Session.Close();
        });
    }

    [RelayCommand]
    private async Task ShowFileExportDialog()
    {
        var dirs = await _dialogService.ShowFolderPicker(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Export Destination",
        });

        if (dirs?.Count == 1)
        {
            var destinationDirectory = dirs[0].Path.ToPath();
            if (Directory.Exists(destinationDirectory))
            {
                var vm = _fileExportViewModelFactory();
                vm.DestinationDirectory = destinationDirectory;
                vm.SelectedItems = SelectedSearchResults;
                _ = await _dialogService.ShowDialog(vm, onOpen: (o, e) =>
                {
                    vm.CloseDialog = () => e.Session.Close();
                });
            }
        }
    }

    [RelayCommand]
    public async Task RemoveSelectedItems()
    {
        var confirmationMessage = $"Are you sure you want to remove the selected {"images".ToQuantity(SelectedSearchResults.Count)}?" +
            Environment.NewLine +
            Environment.NewLine +
            "Files on disk will not be altered or deleted.";

        if (await _dialogService.ShowConfirmationDialog(confirmationMessage))
        {
            try
            {
                IsBusy = true;

                await Task.Run(() =>
                {
                    Messenger.Send(new RemoveImageFiles
                    {
                        ImageFiles = SelectedSearchResults,
                    });
                });
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void SearchObjectName(string? objectName)
    {
        if (objectName is null) return;

        Messenger.Send(new ObjectNameSearchFill($"\"{objectName}\""));
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
            await _systemService.OpenInExplorer(fileInfo.FullName);
        }
    }

    [RelayCommand]
    private async Task OpenInPixInsight(ImageFileViewModel imageFile)
    {
        IFileInfo fileInfo = _fileSystem.FileInfo.New(imageFile.Path);
        if (fileInfo.Exists)
        {
            // TODO: Application setting to specify PixInsight path?

            string pixPath = @"C:\Program Files\PixInsight\bin\PixInsight.exe";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                pixPath = @"/Applications/PixInsight/PixInsight.app/Contents/MacOS/PixInsight";
            }

            await _systemService.StartProcess(pixPath, $"\"{fileInfo.FullName}\"");
        }
    }

    [RelayCommand]
    private async Task CopyIntegrationSummary()
    {
        if (StatsView is null) return;

        var sb = new StringBuilder(512);

        sb.Append("Total Integration: ");
        sb.Append(TotalIntegration);
        sb.Append(" hr");
        sb.AppendLine();

        // Use StatsView because it maintains the user's sort.
        foreach (var item in StatsView.OfType<IntegrationStatistic>())
        {
            sb.Append(item.Filter);
            sb.Append(": ");
            sb.Append(item.TotalIntegrationDisplay);
            sb.Append(" hr");
            sb.AppendLine();
        }

        var summary = sb.ToString().TrimEnd();
        if (summary.Length > 0)
        {
            await _systemService.SetClipboard(summary);
        }
    }

    [RelayCommand]
    private async Task CopyIntegrationSummaryAsMarkdown()
    {
        if (StatsView is null) return;

        const string FilterTitle = "Filter";
        const string ExposureTitle = "Exposures";
        const string IntegrationTitle = "Integration";
        const string IntegrationUnit = " hr";

        int filterSize = FilterTitle.Length;
        int integrationSize = IntegrationTitle.Length;
        int exposureSize = ExposureTitle.Length;

        double totalIntegration = 0;
        foreach (var item in StatsView.OfType<IntegrationStatistic>()) {
            filterSize = Math.Max(item.Filter?.Length ?? 0, filterSize);
            exposureSize = Math.Max(item.Count.ToString().Length, exposureSize);
            integrationSize = Math.Max(item.TotalIntegrationDisplay.Length, integrationSize);
            totalIntegration += item.TotalIntegration.TotalHours;
        }

        var totalIntegrationString = totalIntegration.ToString("F1") + IntegrationUnit;
        integrationSize = Math.Max(totalIntegrationString.Length, integrationSize);

        var sb = new StringBuilder(512);
        string formatString = $"| {{0,{-1 * filterSize}}} | {{1,{exposureSize}}} | {{2,{integrationSize}}} |";
        const int SpaceWidth = 2;

        sb.Append(string.Format(formatString, FilterTitle, ExposureTitle, IntegrationTitle));
        sb.AppendLine();
            
        sb.Append("|");
        sb.Append('-', filterSize + SpaceWidth);
        sb.Append("|");
        sb.Append('-', exposureSize + SpaceWidth - 1);
        sb.Append(":|");
        sb.Append('-', integrationSize + SpaceWidth - 1);
        sb.Append(":|");
        sb.AppendLine();


        foreach (var item in StatsView.OfType<IntegrationStatistic>())
        {
            sb.Append(string.Format(formatString, item.Filter, item.Count, item.TotalIntegrationDisplay + IntegrationUnit));
            sb.AppendLine();
        }

        sb.Append(string.Format(formatString, "Total", "", totalIntegrationString));


        var summary = sb.ToString().TrimEnd();
        if (summary.Length > 0)
        {
            await _systemService.SetClipboard(summary);
        }
    }
}

public class IntegrationStatistic
{
    public string? Filter { get; init; }
    public int Count { get; init; }
    public TimeSpan TotalIntegration { get; init; }

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