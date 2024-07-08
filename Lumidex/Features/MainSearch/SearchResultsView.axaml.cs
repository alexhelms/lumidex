using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace Lumidex.Features.MainSearch;
public partial class SearchResultsView : UserControl
{
    public SearchResultsView()
    {
        InitializeComponent();
    }

    private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is DataGrid dataGrid)
        {
            if (dataGrid.SelectedItems.Count > 1)
            {
                dataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.Collapsed;
            }
            else
            {
                dataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
            }
        }
    }

    private async void RemoveTagMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SearchResultsViewModel vm &&
            e.Source is Avalonia.Controls.MenuItem menuItem &&
            menuItem.DataContext is TagViewModel tagVm)
        {
            if (menuItem.GetLogicalParent<ContextMenu>() is { } contextMenu &&
                contextMenu.Tag is ImageFileViewModel imageFileVm)
            {
                await vm.RemoveTag(imageFileVm.Id, tagVm.Id);
            }
        }
    }
}
