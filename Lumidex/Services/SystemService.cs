using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Input.Platform;
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
            await StartProcess("explorer", $"\"{url}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await StartProcess("open", $"-u \"{url}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await LaunchDetached("xdg-open", url);
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
            await StartProcess("explorer", $"/select,\"{path}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await StartProcess("open", $"-R \"{path}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await ShowInFileManager(path);
        }
        else
        {
            await _dialogService.ShowMessageDialog($"{nameof(OpenInExplorer)} not implemented for {RuntimeInformation.OSDescription}");
        }
    }

    // Linux equivalent of "reveal in file manager". Windows /select and macOS -R
    // both highlight the file inside its folder; the freedesktop
    // org.freedesktop.FileManager1.ShowItems D-Bus method does the same and is
    // honored by Dolphin, Nautilus, Nemo and Caja. When that interface isn't
    // available (a minimal file manager, no D-Bus session) the call exits
    // non-zero and we fall back to opening the containing folder with xdg-open,
    // so the action still does something useful everywhere.
    private async Task ShowInFileManager(string path)
    {
        // ShowItems takes file:// URIs. TryCreate yields a percent-encoded
        // absolute URI (handling spaces etc.) and returns false rather than
        // throwing for a relative/malformed path — in which case we skip D-Bus
        // and just open the containing folder.
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            // Args go through ArgumentList (not a single string) so the GVariant
            // array literal and empty startup-id reach gdbus verbatim, without
            // the shell-quoting pitfalls of the Process.Start(file, args) overload.
            var startInfo = new ProcessStartInfo("gdbus") { UseShellExecute = false };
            startInfo.ArgumentList.Add("call");
            // Bound the wait so a wedged FileManager1 provider can't hang the
            // reveal — gdbus exits non-zero on timeout, routing us to the
            // xdg-open fallback below.
            startInfo.ArgumentList.Add("--timeout");
            startInfo.ArgumentList.Add("5");
            startInfo.ArgumentList.Add("--session");
            startInfo.ArgumentList.Add("--dest");
            startInfo.ArgumentList.Add("org.freedesktop.FileManager1");
            startInfo.ArgumentList.Add("--object-path");
            startInfo.ArgumentList.Add("/org/freedesktop/FileManager1");
            startInfo.ArgumentList.Add("--method");
            startInfo.ArgumentList.Add("org.freedesktop.FileManager1.ShowItems");
            // uris: a one-element array. AbsoluteUri is percent-encoded, so any
            // ", [ or ] in the source path is escaped and can't break out of the
            // GVariant literal to inject extra array elements or arguments.
            startInfo.ArgumentList.Add($"[\"{uri.AbsoluteUri}\"]");
            startInfo.ArgumentList.Add("\"\"");                      // startup-id: empty

            if (await RunToCompletion(startInfo))
                return;
        }

        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
            await LaunchDetached("xdg-open", parent);
    }

    // Runs a process to completion and reports whether it exited successfully.
    // StartProcess is fire-and-forget; ShowInFileManager needs the exit code to
    // know whether the D-Bus reveal worked before deciding to fall back.
    private async Task<bool> RunToCompletion(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to run {Process}", startInfo.FileName);
            return false;
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

    // Fire-and-forget launch that passes each argument individually through
    // ArgumentList. StartProcess takes a single arguments string, which is fine
    // for the fixed Windows/macOS invocations but tokenizes embedded quotes —
    // a path or URL containing a `"` can split into extra argv elements there.
    // The Linux open/reveal actions feed user-derived paths, so they go through
    // this overload instead, where each argument stays intact.
    private async Task LaunchDetached(string executable, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start {Process}", executable);
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
