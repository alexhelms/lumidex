using Avalonia.Controls;

namespace Lumidex;

public interface IViewAware
{
    Control? View { get; }

    void AttachView(Control view);
}
