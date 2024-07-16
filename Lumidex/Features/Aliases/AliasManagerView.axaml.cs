using Avalonia.Controls;

namespace Lumidex.Features.Aliases;
public partial class AliasManagerView : UserControl
{
    public AliasManagerView()
    {
        InitializeComponent();
    }

    private void NewAliasTextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter &&
            e.KeyModifiers == Avalonia.Input.KeyModifiers.None)
        {
            if (ObjectNameDataGrid.DataContext is AliasManagerViewModel aliasManager)
            {
                // I'd like to do this with a behavior but without handling the event,
                // the enter key goes to the next datagrid row which is not desirable.
                aliasManager.CreateAliasCommand.Execute(ObjectNameDataGrid.SelectedItem);
                e.Handled = true;
            }
        }
    }
}
