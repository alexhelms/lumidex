using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex;

public interface IViewModelBase { }

public class ViewModelBase : ObservableRecipient, IViewModelBase, IActivatable, IViewAware, IRequestClose
{
    private bool _hasRegisteredAllMessages = false;
    private bool _initialActivate = true;

    /// <summary>
    /// When true, the messenger is unregistered when the view model is deactivated.
    /// </summary>
    public bool UnregisterMessengerWhenDeactivated { get; protected set; }

    public Control? View { get; private set; }

    protected ViewModelBase()
    {
        Messenger.RegisterAll(this);
        _hasRegisteredAllMessages = true;
    }

    public void AttachView(Control view)
    {
        View = view;
    }

    protected virtual void OnInitialActivated() { }

    protected override void OnActivated()
    {
        if (!_hasRegisteredAllMessages)
        {
            base.OnActivated();
            _hasRegisteredAllMessages = true;
        }

        if (_initialActivate)
        {
            OnInitialActivated();
            _initialActivate = false;
        }
    }

    protected override void OnDeactivated()
    {
        if (UnregisterMessengerWhenDeactivated)
        {
            base.OnDeactivated();
            _hasRegisteredAllMessages = false;
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
    private bool _hasRegisteredAllMessages = false;
    private bool _initialActivate = true;
    private bool _isActive;

    protected IMessenger Messenger { get; }

    /// <summary>
    /// When true, the messenger is unregistered when the view model is deactivated.
    /// </summary>
    public bool UnregisterMessengerWhenDeactivated { get; protected set; }

    protected ValidatableViewModelBase()
        : this(WeakReferenceMessenger.Default)
    {
        Messenger.RegisterAll(this);
        _hasRegisteredAllMessages = true;
    }

    protected ValidatableViewModelBase(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(nameof(messenger));

        Messenger = messenger;
    }

    protected virtual void OnInitialActivated() { }

    protected virtual void OnActivated()
    {
        if (!_hasRegisteredAllMessages)
        {
            Messenger.RegisterAll(this);
            _hasRegisteredAllMessages = true;
        }
    }

    protected virtual void OnDeactivated()
    {
        if (UnregisterMessengerWhenDeactivated)
        {
            Messenger.UnregisterAll(this);
            _hasRegisteredAllMessages = false;
        }
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