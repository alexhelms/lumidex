using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Lumidex.Features.MainSearch.Messages;

namespace Lumidex.Features.MainSearch;
public partial class SearchResultsView : UserControl
{
    private bool _restoringSelection;

    public SearchResultsView()
    {
        InitializeComponent();

        Loaded += SearchResultsView_Loaded;
    }

    private void SearchResultsView_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // This event fires BEFORE the view model OnActivate.

        // Restore the previously selected items
        if (DataContext is SearchResultsViewModel vm &&
            vm.SelectedSearchResults.Count > 0)
        {
            _restoringSelection = true;
            for (int i = 0; i < vm.SelectedSearchResults.Count; i++)
            {
                SearchResultsDataGrid.SelectedItems.Add(vm.SelectedSearchResults[i]);
            }
            _restoringSelection = false;
        }
    }

    private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_restoringSelection) return;

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

            if (dataGrid.DataContext is SearchResultsViewModel vm)
            {
                // Manually manipulate SelectedSearchResults instead of using DataGridSelectedItemsBehavior
                // because the behavior happens *after* the SelectionChanged event so in the scope of this
                // event handler SelectedSearchResults contains old data.
                foreach (var item in e.RemovedItems.OfType<ImageFileViewModel>())
                {
                    vm.SelectedSearchResults.Remove(item);
                    vm.SelectedSearchResultsCount--;
                }

                foreach (var item in e.AddedItems.OfType<ImageFileViewModel>())
                {
                    vm.SelectedSearchResults.Add(item);
                    vm.SelectedSearchResultsCount++;
                }

                // This message is sent here instead of listening to collection changed events
                // because this message is only sent after all items have been added. Using the
                // collection changed event the items are not added in bulk so one message is sent
                // per item.
                WeakReferenceMessenger.Default.Send(new SelectedSearchResultsChanged(vm.SelectedSearchResults));
            }
        }
    }

    private void RemoveTagMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SearchResultsViewModel vm &&
            e.Source is Avalonia.Controls.MenuItem menuItem &&
            menuItem.DataContext is TagViewModel tagVm)
        {
            if (menuItem.GetLogicalParent<ContextMenu>() is { } contextMenu &&
                contextMenu.Tag is ImageFileViewModel imageFileVm)
            {
                // Note: Right clicking the menu item selects the single row.
                vm.RemoveTagCommand.Execute(tagVm);
            }
        }
    }

    private void CopyToClipboard_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        FlyoutBase.ShowAttachedFlyout(CopyGrid);
    }
}
