using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System.Collections;

namespace Lumidex.Behaviors;

/// <summary>
/// Bind to the <see cref="DataGrid.SelectedItems"/> property.
/// </summary>
public class DataGridSelectedItemsBehavior : Behavior<DataGrid>
{
    private IList _selectedItems = new List<object>();

    public static readonly DirectProperty<DataGridSelectedItemsBehavior, IList> SelectedItemsProperty =
        AvaloniaProperty.RegisterDirect<DataGridSelectedItemsBehavior, IList>(
            nameof(AssociatedObject.SelectedItem),
            o => o._selectedItems,
            (o, v) => o._selectedItems = v,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public IList SelectedItems
    {
        get => GetValue(SelectedItemsProperty);
        set => SetAndRaise(SelectedItemsProperty, ref _selectedItems, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(DataGrid.SelectionChangedEvent, AssociatedObject_SelectionChanged, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(DataGrid.SelectionChangedEvent, AssociatedObject_SelectionChanged);
    }

    private void AssociatedObject_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var item in e.RemovedItems)
            _selectedItems.Remove(item);

        foreach (var item in e.AddedItems)
            _selectedItems.Add(item);
    }
}
