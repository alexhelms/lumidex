using Avalonia.Threading;
using Humanizer;
using Lumidex.Core.IO;
using Lumidex.Services;

namespace Lumidex.Features.FileExport;

public partial class FileExportViewModel : ViewModelBase
{
    private readonly DialogService _dialogService;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    public partial string DestinationDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollectionEx<ImageFileViewModel> SelectedItems { get; set; } = new();

    [ObservableProperty]
    public partial long BytesTransferred { get; set; }

    [ObservableProperty]
    public partial long TotalBytes { get; set; }

    [ObservableProperty]
    public partial int FilesTransferredCount { get; set; }

    [ObservableProperty]
    public partial int TotalFilesCount { get; set; }

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    public partial string? DetailedProgressText { get; set; }
    public Action CloseDialog { get; set; } = () => { };

    public FileExportViewModel(DialogService dialogService)
    {
        _dialogService = dialogService;
    }

    protected override void OnInitialActivated()
    {
        base.OnInitialActivated();
        ExportFilesCommand.ExecuteAsync(null);
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        // Cancel the export operation if the user closes the dialog.
        _cts.Cancel();
    }

    [RelayCommand(IncludeCancelCommand = true)]
    public async Task ExportFiles(CancellationToken token)
    {
        if (!Directory.Exists(DestinationDirectory)) return;

        var exporter = new FileExporter();
        var filenames = SelectedItems.Select(f => f.Path).ToList();

        var progress = new Progress<FileExporterProgress>(p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                BytesTransferred = p.CurrentBytes;
                TotalBytes = p.TotalBytes;
                FilesTransferredCount = p.CurrentFileCount;
                TotalFilesCount = p.TotalFilesCount;

                var itemsRemaining = TotalFilesCount - FilesTransferredCount;
                var mbRemaining = (TotalBytes - BytesTransferred).Bytes().Megabytes.Megabytes().Humanize();
                var percent = (double) BytesTransferred / TotalBytes * 100.0;
                ProgressText = $"{percent:F1}%";
                DetailedProgressText = $"{itemsRemaining} items remaining ({mbRemaining})";
            });
        });

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token);

        try
        {
            int filesCopied = await exporter.CopyAsync(filenames, DestinationDirectory,
                progress: progress,
                token: linkedCts.Token);
            if (filesCopied != filenames.Count && !linkedCts.Token.IsCancellationRequested)
            {
                int filesNotCopied = filenames.Count - filesCopied;
                await _dialogService.ShowMessageDialog(
                    $"{filesNotCopied} files were not found and not exported." +
                    Environment.NewLine + Environment.NewLine +
                    "See the log for which files.");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            await _dialogService.ShowMessageDialog(
                "An error occurred while exporting files." +
                Environment.NewLine + Environment.NewLine +
                e.Message);
        }
        finally
        {
            CloseDialog();
        }
    }
}
