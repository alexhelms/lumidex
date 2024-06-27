using Avalonia.Controls;

namespace Lumidex;

public class ViewModelBase : ObservableRecipient, IActivatable, IViewAware, IRequestClose
{
    private bool _initialActivate = true;

    public Control? View { get; private set; }

    public void AttachView(Control view)
    {
        View = view;
    }

    protected virtual void OnInitialActivated() { }

    protected override void OnActivated()
    {
        base.OnActivated();

        if (_initialActivate)
        {
            OnInitialActivated();
            _initialActivate = false;
        }
    }

    public async Task RequestCloseAsync()
    {
        if (View is Window window)
        {
            if (await CanCloseAsync())
                window.Close();
        }
    }

    public virtual Task<bool> CanCloseAsync()
    {
        return Task.FromResult(true);
    }
}
