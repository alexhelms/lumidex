using Avalonia.Controls;
using Lumidex.Core.Data;
using Lumidex.Core.Pipelines;
using Lumidex.Features.Library.Messages;
using Lumidex.Validation;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Lumidex.Features.Library;

public partial class LibraryViewModel : ValidatableViewModelBase
{
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;
    private readonly Func<LibraryIngestPipeline> _pipelineFactory;

    [ObservableProperty] int _id;
    [ObservableProperty] int _fileCount;
    [ObservableProperty] bool _scanning;
    [ObservableProperty] bool _progressIndeterminate;
    [ObservableProperty] bool _showProgress;
    [ObservableProperty] bool _scanResultsAvailable;
    [ObservableProperty] int _scanTotalCount;
    [ObservableProperty] int _scanProgressCount;
    [ObservableProperty] string? _scanSummary;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickScanLibraryCommand))]
    DateTime? _lastScan;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(3, ErrorMessage = "3 character minimum.")]
    [MaxLength(128, ErrorMessage = "128 character maximum.")]
    [NotifyCanExecuteChangedFor(nameof(ScanLibraryCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuickScanLibraryCommand))]
    string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Path is required.")]
    [FolderExists]
    [NotifyCanExecuteChangedFor(nameof(ScanLibraryCommand))]
    [NotifyCanExecuteChangedFor(nameof(QuickScanLibraryCommand))]
    string _path = string.Empty;

    public ObservableCollectionEx<FileErrorViewModel> ScanErrors { get; } = new();

    public LibraryViewModel(
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        Func<LibraryIngestPipeline> pipelineFactory)
    {
        _dbContextFactory = dbContextFactory;
        _pipelineFactory = pipelineFactory;
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = dbContext.Libraries
            .AsNoTracking()
            .FirstOrDefault(l => l.Id == Id);

        if (library is not null)
        {
            Name = library.Name;
            LastScan = library.LastScan;
            Path = library.Path;
        }

        FileCount = dbContext.ImageFiles.Count(f => f.LibraryId == Id);
        OnPropertyChanged(nameof(CanQuickScanLibrary));
    }

    partial void OnNameChanged(string? oldValue, string newValue)
    {
        if (IsActive && HasErrors == false)
        {
            SaveChanges();
        }
    }

    partial void OnPathChanged(string? oldValue, string newValue)
    {
        if (IsActive && HasErrors == false)
        {
            SaveChanges();
        }
    }

    private void SaveChanges()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = dbContext.Libraries.FirstOrDefault(l => l.Id == Id);
        if (library is not null)
        {
            library.Name = Name;
            library.Path = Path;
            dbContext.SaveChanges();
        }
    }

    private async Task ScanLibraryAsync(bool quickScan, CancellationToken token)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = dbContext.Libraries
            .AsNoTracking()
            .First(l => l.Id == Id);

        try
        {
            Scanning = false;
            ProgressIndeterminate = true;
            ShowProgress = true;
            ScanProgressCount = 0;
            ScanTotalCount = 1;
            ScanSummary = null;
            ScanErrors.Clear();

            var pipeline = _pipelineFactory();
            ScanTotalCount = await pipeline.GetEstimatedTotalFiles(
                library: library,
                forceFullScan: quickScan == false);

            var progress = new Progress<IngestProgress>(p =>
            {
                ProgressIndeterminate = false;
                ScanProgressCount += p.TotalCount;
            });
            
            Scanning = true;
            await pipeline.ProcessAsync(library,
                forceFullScan: quickScan == false,
                progress: progress,
                token: token);

            var added = pipeline.Added.Count;
            var skipped = pipeline.Skipped.Count;
            var errors = pipeline.Errors.Count;
            var elapsed = pipeline.Elapsed;
            ScanSummary = $"{added} added, {skipped} skipped, {errors} errors in {elapsed.TotalSeconds:F3} seconds";
            ScanErrors.AddRange(pipeline.Errors
                .Select(error => new FileErrorViewModel
                {
                    Path = error.FileInfo.FullName,
                    Error = error.Message,
                }));

            Messenger.Send(new LibraryScanned
            {
                Library = new Common.LibraryViewModel
                {
                    Id = library.Id,
                    Name = library.Name,
                    Path = library.Path,
                    LastScan = library.LastScan,
                }
            });
        }
        catch (OperationCanceledException)
        {
            ScanSummary = "Scan canceled.";
        }
        finally
        {
            Scanning = false;
            ProgressIndeterminate = false;
            ShowProgress = false;
        }

        // Refresh basic library stats
        library = dbContext.Libraries
            .AsNoTracking()
            .First(l => l.Id == Id);

        LastScan = library.LastScan;
        FileCount = dbContext.ImageFiles.Count(f => f.LibraryId == Id);
        OnPropertyChanged(nameof(CanQuickScanLibrary));
    }

    [RelayCommand]
    private async Task ChangeLibraryPath()
    {
        // TODO: move file dialog operations into a service.
        if (TopLevel.GetTopLevel(View) is { } topLevel)
        {
            var startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(Path));
            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new()
            {
                AllowMultiple = false,
                SuggestedStartLocation = startLocation,
                Title = $"{Name} Library",
            });

            if (folder.Count == 1)
            {
                Path = Uri.UnescapeDataString(folder[0].Path.AbsolutePath);
                SaveChanges();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanLibrary), IncludeCancelCommand = true)]
    private async Task ScanLibrary(CancellationToken token)
    {
        await ScanLibraryAsync(quickScan: false, token);
    }

    public bool CanScanLibrary => !HasErrors && !Scanning;

    [RelayCommand(CanExecute = nameof(CanQuickScanLibrary), IncludeCancelCommand = true)]
    private async Task QuickScanLibrary(CancellationToken token)
    {
        await ScanLibraryAsync(quickScan: true, token);
    }

    public bool CanQuickScanLibrary => !HasErrors && !Scanning && LastScan is not null; 

    [RelayCommand(CanExecute = nameof(CanDeleteLibrary))]
    private void DeleteLibrary()
    {
        // TODO: Confirmation dialog since this is a destructive action.

        Messenger.Send(new DeleteLibrary
        { 
            Library = new Common.LibraryViewModel
            {
                Id = Id,
                Name = Name,
                Path = Path,
                LastScan = LastScan,
            },
        });
    }

    public bool CanDeleteLibrary
    {
        get
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return dbContext.Libraries.Count() > 1;
        }
    }
}

public partial class FileErrorViewModel : ViewModelBase
{
    [ObservableProperty] string? _path;
    [ObservableProperty] string? _error;
}