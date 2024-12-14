namespace Lumidex.Features.SideNavBar;

public partial class SideNavBarItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ToolTipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Icon { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
