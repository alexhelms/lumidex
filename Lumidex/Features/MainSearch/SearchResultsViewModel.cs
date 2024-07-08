using AutoMapper;
using AutoMapper.QueryableExtensions;
using Avalonia.Threading;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Messages;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;

namespace Lumidex.Features.MainSearch;

public partial class SearchResultsViewModel : ViewModelBase,
    IRecipient<QueryMessage>
{
    private readonly LumidexDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IFileSystem _fileSystem;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty] bool _isSearching;
    [ObservableProperty] string? _totalIntegration;
    [ObservableProperty] string? _typeAggregate;
    [ObservableProperty] int _lightCount;
    [ObservableProperty] int _flatCount;
    [ObservableProperty] int _darkCount;
    [ObservableProperty] int _biasCount;
    [ObservableProperty] int _unknownCount;
    [ObservableProperty] int _distintObjectNameCount;
    [ObservableProperty] AvaloniaList<TagViewModel> _allTags = new();
    [ObservableProperty] AvaloniaList<ImageFileViewModel> _searchResults = new();
    [ObservableProperty] AvaloniaList<ImageFileViewModel> _selectedSearchResults = new();
    [ObservableProperty] AvaloniaList<IntegrationStatistic> _integrationStats = new();

    public SearchResultsViewModel(
        LumidexDbContext dbContext,
        IMapper mapper,
        IFileSystem fileSystem)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _fileSystem = fileSystem;
    }

    ~SearchResultsViewModel()
    {
        _searchCts?.Dispose();
    }

    private async Task<List<ImageFile>> GetSelectedImageFiles()
    {
        var imageFileIds = SelectedSearchResults.Select(x => x.Id).ToHashSet();
        var imageFiles = await _dbContext.ImageFiles
            .Include(f => f.Tags)
            .Where(f => imageFileIds.Contains(f.Id))
            .ToListAsync();

        return imageFiles;
    }

    protected override async void OnActivated()
    {
        base.OnActivated();

        var allTags = await _dbContext.Tags
            .OrderBy(tag => tag.TaggedImages.Count)
            .OrderBy(tag => tag.Name)
            .ProjectTo<TagViewModel>(_mapper.ConfigurationProvider)
            .ToListAsync();

        AllTags = new(allTags);
    }

    public async void Receive(QueryMessage message)
    {
        await Search(message);
    }

    private async Task Search(QueryMessage message)
    {
        // Cancel an existing search and wait for it to complete
        if (IsSearching)
        {
            _searchCts?.Cancel();
            while (IsSearching)
            {
                await Task.Delay(100);
            }
        }

        try
        {
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsSearching = true;
                SearchResults.Clear();
                IntegrationStats.Clear();
                TotalIntegration = null;
                TypeAggregate = null;
                LightCount = 0;
                FlatCount = 0;
                DarkCount = 0;
                BiasCount = 0;
                UnknownCount = 0;
            }, DispatcherPriority.ApplicationIdle);

            var query = _dbContext.ImageFiles.AsNoTracking();

            if (message.Library is not null)
                query = query.Where(f => f.LibraryId == message.Library.Id);

            if (message.ObjectName is { Length: >0 })
                query = query.Where(f => EF.Functions.Like(f.ObjectName, $"%{message.ObjectName}%"));

            if (message.ImageType is { } imageType)
                query = query.Where(f => f.Type == imageType);

            if (message.ImageKind is { } imageKind )
                query = query.Where(f => f.Kind == imageKind);

            if (message.ExposureMin is { } min)
                query = query.Where(f => f.Exposure!.Value >= min.TotalSeconds);

            if (message.ExposureMax is { } max)
                query = query.Where(f => f.Exposure!.Value <= max.TotalSeconds);

            if (message.Filter is { Length: > 0 } filter)
                query = query.Where(f => f.FilterName == filter);

            if (message.DateBegin is { } dateBegin)
                query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);

            if (message.DateEnd is { } dateEnd)
                query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);

            if (message.TagIds is { Length: > 0})
            {
                var tagIds = message.TagIds.ToHashSet();
                query = query.Where(f => f.Tags.Any(tag => tagIds.Contains(tag.Id)));
            }

            var results = await Task.Run(async () =>
                await query
                    .Include(f => f.Library)
                    .Include(f => f.Tags)
                    .ProjectTo<ImageFileViewModel>(_mapper.ConfigurationProvider)
                    .ToListAsync(_searchCts.Token));

            var stats = ComputeIntegrationStatistics(results);
            IntegrationStats.AddRange(stats);
            SearchResults.AddRange(results);

            double totalIntegrationSum = IntegrationStats.Sum(x => x.TotalIntegration.TotalHours);
            TotalIntegration = totalIntegrationSum < 1
                ? totalIntegrationSum.ToString("F2")
                : totalIntegrationSum.ToString("F1");

            foreach (var item in SearchResults)
            {
                if (item.Type == ImageType.Light) LightCount++;
                else if (item.Type == ImageType.Flat) FlatCount++;
                else if (item.Type == ImageType.Dark) DarkCount++;
                else if (item.Type == ImageType.Bias) BiasCount++;
                else UnknownCount++;
            }
            TypeAggregate = string.Join('/', LightCount, FlatCount, DarkCount, BiasCount, UnknownCount);
            DistintObjectNameCount = results
                .Select(x => x.ObjectName)
                .Where(x => x is not null)
                .Distinct()
                .Count();
        }
        catch (OperationCanceledException) { }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSearching = false);
        }
    }

    private static IEnumerable<IntegrationStatistic> ComputeIntegrationStatistics(List<ImageFileViewModel> images)
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

        return new AvaloniaList<IntegrationStatistic>(results);
    }

    [RelayCommand]
    private async Task AddTagToSelection(int tagId)
    {
        var tag = await _dbContext.Tags.FirstOrDefaultAsync(tag => tag.Id == tagId);
        if (tag is null) return;

        var imageFiles = await GetSelectedImageFiles();

        foreach (var imageFile in imageFiles)
        {
            imageFile.Tags.Add(tag);
        }

        if (await _dbContext.SaveChangesAsync() > 0)
        {
            // Add the item to the UI
            var tagVm = AllTags.First(tag => tag.Id == tagId);
            foreach (var item in SelectedSearchResults)
            {
                item.Tags.Add(tagVm);
                item.Tags = new(item.Tags.Distinct());
            }
        }
    }

    public async Task RemoveTag(int imageFileId, int tagId)
    {
        // This is not a [RelayCommand] because it requires two parameters.
        // This function is invoked when the user right clicks an individual
        // tag on an image file in a row and selects "Remove Tag".
        
        var imageFile = await _dbContext.ImageFiles
            .Include(f => f.Tags)
            .Where(f => f.Id == imageFileId)
            .FirstOrDefaultAsync();

        if (imageFile is not null)
        {
            var tag = imageFile.Tags.FirstOrDefault(tag => tag.Id == tagId);
            if (tag is not null)
            {
                imageFile.Tags.Remove(tag);
            }
        }

        if (await _dbContext.SaveChangesAsync() > 0)
        {
            // Remove the item from the UI
            var imageFileVm = SearchResults.FirstOrDefault(f => f.Id == imageFileId);
            if (imageFileVm is not null)
            {
                var tagVm = imageFileVm.Tags.FirstOrDefault(tag => tag.Id == tagId);
                if (tagVm is not null)
                {
                    imageFileVm.Tags.Remove(tagVm);
                }
            }
        }
    }

    [RelayCommand]
    private async Task RemoveAllTagsFromSelection()
    {
        var imageFiles = await GetSelectedImageFiles();
        foreach (var imageFile in imageFiles)
        {
            imageFile.Tags.Clear();
        }

        if (await _dbContext.SaveChangesAsync() > 0)
        {
            // Remove tags from the UI
            foreach (var item in SelectedSearchResults)
            {
                item.Tags.Clear();
            }
        }
    }

    [RelayCommand]
    private void OpenInExplorer(ImageFileViewModel imageFile)
    {
        IFileInfo fileInfo = _fileSystem.FileInfo.New(imageFile.Path);
        if (fileInfo.Exists)
        {
            OpenFileInProcess("explorer.exe", Path.GetDirectoryName(fileInfo.FullName)!);
        }
    }

    [RelayCommand]
    private void OpenInPixInsight(ImageFileViewModel imageFile)
    {
        IFileInfo fileInfo = _fileSystem.FileInfo.New(imageFile.Path);
        if (fileInfo.Exists)
        {
            // TODO: Application setting to specify PixInsight.exe path?
            OpenFileInProcess(@"C:\Program Files\PixInsight\bin\PixInsight.exe", fileInfo.FullName);
        }
    }

    private static void OpenFileInProcess(string process, string argument)
    {
        // TODO: move file dialog operations into a service.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                Process.Start(process, argument);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to start {Process} {Argument}", process, argument);
            }
        }
        else
        {
            // TODO: show a message box instead of logging...
            Log.Error("Show In Explorer not implemented for {OS}", RuntimeInformation.OSDescription);
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