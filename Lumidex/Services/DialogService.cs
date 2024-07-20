using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaDialogs.Views;

namespace Lumidex.Services;

public class DialogService
{
    public async Task<IReadOnlyList<IStorageFolder>> ShowFolderPicker(FolderPickerOpenOptions options, string? startLocation = null)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            TopLevel.GetTopLevel(desktop.MainWindow) is { } topLevel)
        {
            if (startLocation is { } && options.SuggestedStartLocation is null)
            {
                options.SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(startLocation));
            }
            
            return await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        }

        return [];
    }

    public async Task<bool> ShowConfirmationDialog(string message)
    {
        var dialog = new TwofoldDialog
        {
            Message = message,
            PositiveText = "Yes",
            NegativeText = "Cancel",
        };
        var result = await dialog.ShowAsync();
        return result.GetValueOrDefault();
    }

    public async Task ShowMessageDialog(string message)
    {
        var dialog = new SingleActionDialog
        {
            Message = message,
            ButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}
