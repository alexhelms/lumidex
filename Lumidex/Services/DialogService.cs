using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using AvaloniaDialogs.Views;
using DialogHostAvalonia;

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

    public async Task<object?> ShowDialog<T>(T viewModel)
        where T : IViewModelBase
    {
        return await DialogHost.Show(viewModel);
    }

    public async Task<object?> ShowDialog<T>(T viewModel, DialogOpenedEventHandler onOpen)
        where T : IViewModelBase
    {
        return await DialogHost.Show(viewModel,
            openedEventHandler: onOpen);
    }

    public async Task<object?> ShowDialog<T>(T viewModel, DialogClosingEventHandler onClosing)
        where T : IViewModelBase
    {
        return await DialogHost.Show(viewModel,
            closingEventHandler: onClosing);
    }

    public async Task<object?> ShowDialog<T>(T viewModel, DialogOpenedEventHandler onOpen, DialogClosingEventHandler onClosing)
        where T : IViewModelBase
    {
        return await DialogHost.Show(viewModel,
            openedEventHandler: onOpen,
            closingEventHandler: onClosing);
    }
}
