using Lumidex.Core.Data;
using Lumidex.Core.Pipelines;
using Lumidex.Features.Library.Messages;
using Lumidex.Services;
using Lumidex.Validation;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;

namespace Lumidex.Features.Library;

public partial class LibraryViewModel : ValidatableViewModelBase
{
    private readonly IFileSystem _fileSystem;
    private readonly DialogService _dialogService;
    private readonly SystemService _systemService;
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
    [ObservableProperty] ObservableCollectionEx<FileErrorViewModel>? _scanErrors;

    [ObservableProperty] DataGridCollectionView? _duplicatesView;
    [ObservableProperty] bool _onlyDuplicateLights = true;

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

    public LibraryViewModel(
        IFileSystem fileSystem,
        DialogService dialogService,
        SystemService systemService,
        IDbContextFactory<LumidexDbContext> dbContextFactory,
        Func<LibraryIngestPipeline> pipelineFactory)
    {
        _fileSystem = fileSystem;
        _dialogService = dialogService;
        _systemService = systemService;
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

        RefreshDuplicates();
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

    partial void OnOnlyDuplicateLightsChanged(bool value)
    {
        DuplicatesView?.Refresh();
    }

    private void RefreshDuplicates()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var duplicateImageFiles = dbContext.ImageFiles
            .GroupBy(f => f.HeaderHash)
            .Where(grp => grp.Count() > 1)
            .Select(grp => grp.Key)
            .SelectMany(hash => dbContext.ImageFiles.Where(f => f.HeaderHash == hash))
            .Select(f => new DuplicateHashViewModel
            {
                Id = f.Id,
                Group = f.HeaderHash,
                Filename = f.Path,
                ImageType = f.Type,
            })
            .ToList();

        // EF cant translate this in the select above so set the group name to something better for the user.
        int groupCount = 1;
        foreach (var group in duplicateImageFiles.GroupBy(vm => vm.Group))
        {
            foreach (var vm in group)
            {
                vm.Group = $"Group {groupCount}";
            }

            groupCount++;
        }

        DuplicatesView = new(duplicateImageFiles);
        DuplicatesView.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(DuplicateHashViewModel.Group)));
        DuplicatesView.Filter = obj =>
        {
            if (obj is DuplicateHashViewModel duplicate)
            {
                if (OnlyDuplicateLights && duplicate.ImageType != ImageType.Light)
                    return false;
            }

            return true;
        };
    }

    private void SaveChanges()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var library = dbContext.Libraries.FirstOrDefault(l => l.Id == Id);
        if (library is not null)
        {
            library.Name = Name;
            library.Path = Path;
            if (dbContext.SaveChanges() > 0)
            {
                var vm = new Common.LibraryViewModel
                {
                    Id = library.Id,
                    Name = library.Name,
                    Path = library.Path,
                    LastScan = library.LastScan,
                };
                Messenger.Send(new LibraryEdited { Library = vm });
            }
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
            ScanErrors = null;
            DuplicatesView = null;

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
            var updated = pipeline.Updated.Count;
            var skipped = pipeline.Skipped.Count;
            var errors = pipeline.Errors.Count;
            var elapsed = pipeline.Elapsed;
            ScanSummary = $"{added} added, {updated} updated, {skipped} skipped, {errors} errors in {elapsed.TotalSeconds:F3} seconds";
            ScanErrors = new(pipeline.Errors
                .Select(error => new FileErrorViewModel
                {
                    Error = error.Message,
                    Filename = error.FileInfo.FullName,
                }));
            RefreshDuplicates();

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
        var folder = await _dialogService.ShowFolderPicker(
            new()
            {
                AllowMultiple = false,
                Title = $"{Name} Library",
            },
            startLocation: Path);

        if (folder.Count == 1)
        {
            Path = Uri.UnescapeDataString(folder[0].Path.AbsolutePath);
            SaveChanges();
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
    private async Task DeleteLibrary()
    {
        if (await _dialogService.ShowConfirmationDialog("Are you sure you want to delete this library?"))
        {
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
    }

    public bool CanDeleteLibrary
    {
        get
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return dbContext.Libraries.Count() > 1;
        }
    }

    [RelayCommand]
    private async Task OpenInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        IFileInfo fileInfo = _fileSystem.FileInfo.New(path);
        if (fileInfo.Exists)
        {
            await _systemService.OpenInExplorer(fileInfo.FullName);
        }
    }

    [RelayCommand]
    private async Task ScrubDuplicates()
    {
        if (DuplicatesView is null || DuplicatesView.Count == 0) return;

        var confirmationMessage = "Are you sure you want to scrub the database of duplicates for this library?" +
            Environment.NewLine + Environment.NewLine +
            "Duplicate image files will be removed from the database." +
            Environment.NewLine + Environment.NewLine +
            "No file on disk will be deleted.";

        if (await _dialogService.ShowConfirmationDialog(confirmationMessage))
        {
            await Task.Run(() =>
            {
                var idsToRemove = new HashSet<int>(DuplicatesView.Count);

                foreach (var item in DuplicatesView.OfType<DuplicateHashViewModel>().Where(vm => vm.Filename != null))
                {
                    var fileInfo = _fileSystem.FileInfo.New(item.Filename!);
                    if (!fileInfo.Exists)
                        idsToRemove.Add(item.Id);
                }

                using var dbContext = _dbContextFactory.CreateDbContext();
                dbContext.ImageFiles
                    .Where(f => idsToRemove.Contains(f.Id))
                    .ExecuteDelete();
            });

            RefreshDuplicates();
        }
    }
}

public partial class FileErrorViewModel : ObservableObject
{
    [ObservableProperty] string? _error;
    [ObservableProperty] string? _filename;
}

public partial class DuplicateHashViewModel : ObservableObject
{
    [ObservableProperty] int _id;
    [ObservableProperty] string? _group;
    [ObservableProperty] string? _filename;
    [ObservableProperty] ImageType? _imageType;
}