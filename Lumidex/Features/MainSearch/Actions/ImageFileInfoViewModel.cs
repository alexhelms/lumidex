using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Lumidex.Core.Data;
using Lumidex.Features.MainSearch.Editing.Messages;
using System.Reflection;

namespace Lumidex.Features.MainSearch.Actions;

public partial class ImageFileInfoViewModel : ActionViewModelBase,
    IRecipient<ImageFilesEdited>
{
    private readonly static List<PropertyInfo> UserEditableProperties;

    [ObservableProperty] ObservableCollectionEx<ImageFileInfoItem> _items = new();

    static ImageFileInfoViewModel()
    {
        UserEditableProperties = typeof(ImageFileViewModel)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<UserEditableAttribute>() != null)
            .ToList();
    }

    public ImageFileInfoViewModel()
    {
        DisplayName = "Info";
    }

    protected override void OnSelectedItemsChanged()
    {
        UpdateInfoItems();
    }

    public void Receive(ImageFilesEdited message)
    {
        UpdateInfoItems();
    }

    private void UpdateInfoItems()
    {
        if (SelectedItems.Count == 0)
        {
            Items.Clear();
            return;
        }

        // Create a lookup keyed by property info and the value is a hash set of all values across all image files.
        var lookup = new Dictionary<PropertyInfo, HashSet<object?>>();
        foreach (var item in SelectedItems)
        {
            foreach (var property in UserEditableProperties)
            {
                var value = property.GetValue(item);
                lookup.TryGetValue(property, out var existingValues);
                existingValues ??= [];
                existingValues.Add(value);
                lookup[property] = existingValues;
            }
        }

        // Create the info items
        var items = new List<ImageFileInfoItem>(UserEditableProperties.Count);
        foreach (var property in UserEditableProperties)
        {
            HashSet<object?> values = lookup[property];
            object? value = values.FirstOrDefault();

            var item = new ImageFileInfoItem
            {
                Name = property.Name,
                Value = values.Count > 1 ? "<Multiple Values>" : value?.ToString(),
            };

            items.Add(item);
        }

        Items = new(items.OrderBy(x => x.Name));
    }
}

public partial class ImageFileInfoItem : ObservableObject
{
    [ObservableProperty] string _name = string.Empty;
    [ObservableProperty] string? _value = string.Empty;

    [RelayCommand]
    private async Task Copy()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            TopLevel.GetTopLevel(desktop.MainWindow) is { } topLevel &&
            topLevel.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(Value ?? string.Empty);
        }
    }
}