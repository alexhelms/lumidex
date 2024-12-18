using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ScottPlot;
using ScottPlot.Control;

using MenuItem = Avalonia.Controls.MenuItem;

namespace Lumidex.Controls;

public class AvaPlotMenu : IPlotMenu
{
    private readonly AvaPlot _avaPlot;

    public string DefaultSaveImageFilename { get; set; } = "Plot.png";

    public List<ContextMenuItem> ContextMenuItems { get; set; } = [];

    public AvaPlotMenu(AvaPlot avaPlot)
    {
        _avaPlot = avaPlot;
        Reset();
    }

    public ContextMenuItem[] GetDefaultContextMenuItems()
    {
        ContextMenuItem saveImage = new()
        {
            Label = "Save Image",
            OnInvoke = OpenSaveImageDialog
        };

        // TODO: Copying images to the clipboard is still difficult in Avalonia
        // https://github.com/AvaloniaUI/Avalonia/issues/3588

        return [
            saveImage,
        ];
    }

    public MenuFlyout GetContextMenu()
    {
        var menuFlyout = new MenuFlyout();

        foreach (var curr in ContextMenuItems)
        {
            if (curr.IsSeparator)
            {
                menuFlyout.Items.Add(new MenuItem { Header = "-" });
            }
            else
            {
                var menuItem = new MenuItem { Header = curr.Label };
                menuItem.Click += (s, e) => curr.OnInvoke(_avaPlot);
                menuFlyout.Items.Add(menuItem);
            }
        }

        return menuFlyout;
    }

    public async void OpenSaveImageDialog(IPlotControl plotControl)
    {
        var topLevel = TopLevel.GetTopLevel(_avaPlot) ?? throw new NullReferenceException("Could not find a top level");
        var destinationFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
        {
            SuggestedFileName = DefaultSaveImageFilename,
            FileTypeChoices = filePickerFileTypes
        });

        string? path = destinationFile?.TryGetLocalPath();
        if (path is not null && !string.IsNullOrWhiteSpace(path))
        {
            var lastRenderSize = plotControl.Plot.RenderManager.LastRender.FigureRect.Size;
            plotControl.Plot.Save(path, (int)lastRenderSize.Width, (int)lastRenderSize.Height, ImageFormats.FromFilename(path));
        }
    }

    public readonly List<FilePickerFileType> filePickerFileTypes = [
        new("PNG Files") { Patterns = ["*.png"] },
        new("JPEG Files") { Patterns = ["*.jpg", "*.jpeg"] },
        new("BMP Files") { Patterns = ["*.bmp"] },
        new("WebP Files") { Patterns = ["*.webp"] },
        new("SVG Files") { Patterns = ["*.svg"] },
        new("All Files") { Patterns = ["*"] },
    ];

    public void Add(string Label, Action<IPlotControl> action)
    {
        ContextMenuItems.Add(new ContextMenuItem() { Label = Label, OnInvoke = action });
    }

    public void AddSeparator()
    {
        ContextMenuItems.Add(new ContextMenuItem() { IsSeparator = true });
    }

    public void Clear()
    {
        ContextMenuItems.Clear();
    }

    public void Reset()
    {
        Clear();
        ContextMenuItems.AddRange(GetDefaultContextMenuItems());
    }

    public void ShowContextMenu(Pixel pixel)
    {
        var manualContextMenu = GetContextMenu();
        manualContextMenu.ShowAt(_avaPlot, showAtPointer: true);
    }
}
