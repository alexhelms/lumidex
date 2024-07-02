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
    private readonly LumidexDbContext _dbContext;
    private readonly Func<LibraryIngestPipeline> _pipelineFactory;
    [ObservableProperty] int _id;
    [ObservableProperty] DateTime? _lastScan;
    [ObservableProperty] int _fileCount;
    [ObservableProperty] bool _scanning;
    [ObservableProperty] bool _progressIndeterminate;
    [ObservableProperty] bool _showProgress;
    [ObservableProperty] bool _scanResultsAvailable;
    [ObservableProperty] int _scanTotalCount;
    [ObservableProperty] int _scanProgressCount;
    [ObservableProperty] string? _scanSummary;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MinLength(3, ErrorMessage = "3 character minimum.")]
    [MaxLength(128, ErrorMessage = "128 character minimum.")]
    [NotifyCanExecuteChangedFor(nameof(ScanLibraryCommand))]
    string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Path is required.")]
    [FolderExists]
    [NotifyCanExecuteChangedFor(nameof(ScanLibraryCommand))]
    string _path = string.Empty;

    public AvaloniaList<FileErrorViewModel> ScanErrors { get; } = new();

    public LibraryViewModel(
        LumidexDbContext dbContext,
        Func<LibraryIngestPipeline> pipelineFactory)
    {
        _dbContext = dbContext;
        _pipelineFactory = pipelineFactory;
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        var library = _dbContext.Libraries
            .AsNoTracking()
            .FirstOrDefault(l => l.Id == Id);
        if (library is not null)
        {
            Name = library.Name;
            LastScan = library.LastScan;
            Path = library.Path;
        }

        FileCount = _dbContext.ImageFiles.Count(f => f.LibraryId == Id);
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
        var library = _dbContext.Libraries.FirstOrDefault(l => l.Id == Id);
        if (library is not null)
        {
            library.Name = Name;
            library.Path = Path;
            _dbContext.SaveChanges();
        }
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
                Path = folder[0].Path.AbsolutePath;
                SaveChanges();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanLibrary), IncludeCancelCommand = true)]
    private async Task ScanLibrary(CancellationToken token)
    {
        var library = await _dbContext.Libraries
            .AsNoTracking()
            .FirstAsync(l => l.Id == Id);

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
            ScanTotalCount = await pipeline.GetEstimatedTotalFiles(Path);
            
            var progress = new Progress<IngestProgress>(p =>
            {
                ScanProgressCount += p.TotalCount;
            });

            ProgressIndeterminate = false;
            Scanning = true;
            await pipeline.ProcessAsync(library, progress, token);

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
        library = await _dbContext.Libraries
            .AsNoTracking()
            .FirstAsync(l => l.Id == Id);

        LastScan = library.LastScan;
        FileCount = await _dbContext.ImageFiles.CountAsync(f => f.LibraryId == Id);
    }

    private bool CanScanLibrary => !HasErrors && !Scanning;

    [RelayCommand(CanExecute = nameof(CanDeleteLibrary))]
    private async Task DeleteLibrary()
    {
        // TODO: Confirmation dialog since this is a destructive action.

        var library = await _dbContext.Libraries.FirstAsync(l => l.Id == Id);
        _dbContext.Libraries.Remove(library);
        await _dbContext.SaveChangesAsync();
        Messenger.Send(new LibraryDeleted { Id = Id });
    }

    public bool CanDeleteLibrary => _dbContext.Libraries.Count() > 1;
}

public partial class FileErrorViewModel : ViewModelBase
{
    [ObservableProperty] string? _path;
    [ObservableProperty] string? _error;
}