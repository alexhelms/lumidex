namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _toolTipText = string.Empty;
    [ObservableProperty] private string _icon = string.Empty;
    [ObservableProperty] private bool _isSelected;
}
