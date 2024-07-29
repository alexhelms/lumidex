using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Lumidex.Services;

public class SystemService
{
    private readonly DialogService _dialogService;

    public SystemService(DialogService dialogService)
    {
        _dialogService = dialogService;
    }

    public async Task OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await StartProcess("explorer.exe", url);
        }
        else
        {
            await _dialogService.ShowMessageDialog($"{nameof(OpenUrl)} not implemented for {RuntimeInformation.OSDescription}");
        }
    }

    public async Task OpenInExplorer(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await StartProcess("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            await _dialogService.ShowMessageDialog($"{nameof(OpenInExplorer)} not implemented for {RuntimeInformation.OSDescription}");
        }
    }

    public async Task StartProcess(string executable, string args)
    {
        try
        {
            Process.Start(executable, args);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start {Process} {Argument}", executable, args);
            await _dialogService.ShowMessageDialog($"Failed to start {executable}");
        }
    }

    public async Task SetClipboard(string? value)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            TopLevel.GetTopLevel(desktop.MainWindow) is { } topLevel &&
            topLevel.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(value);
        }
    }
}
