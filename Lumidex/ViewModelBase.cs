using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex;

public interface IViewModelBase { }

public class ViewModelBase : ObservableRecipient, IViewModelBase, IActivatable, IViewAware, IRequestClose
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

public class ValidatableViewModelBase : ObservableValidator, IViewModelBase, IActivatable, IViewAware, IRequestClose
{
    private bool _initialActivate = true;
    private bool _isActive;

    protected IMessenger Messenger { get; }

    protected ValidatableViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
    }

    protected ValidatableViewModelBase(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(nameof(messenger));

        Messenger = messenger;
    }

    protected virtual void OnInitialActivated() { }

    protected virtual void OnActivated()
    {
        Messenger.RegisterAll(this);
    }

    protected virtual void OnDeactivated()
    {
        Messenger.UnregisterAll(this);
    }

    protected virtual void Broadcast<T>(T oldValue, T newValue, string? propertyName)
    {
        PropertyChangedMessage<T> message = new(this, propertyName, oldValue, newValue);

        _ = Messenger.Send(message);
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

    public void AttachView(Control view)
    {
        View = view;
    }

    public Control? View { get; private set; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value, true))
            {
                if (value)
                {
                    if (_initialActivate)
                    {
                        OnInitialActivated();
                        _initialActivate = false;
                    }

                    OnActivated();
                }
                else
                {
                    OnDeactivated();
                }
            }
        }
    }
}