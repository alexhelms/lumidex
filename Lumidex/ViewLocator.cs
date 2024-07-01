using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex;

public class ViewLocator : IDataTemplate
{
    public static ViewLocator Instance { get; } = new();


    public bool Match(object? data)
    {
        return data is IViewModelBase;
    }

    public Control? Build(object? param)
    {
        var name = param?.GetType().FullName?.Replace("ViewModel", "View");
        if (param is null || name is null)
            return new TextBlock { Text = "Invalid Data Type" };

        var type = param.GetType().Assembly.GetTypes()
            .Where(t => t.FullName == name)
            .FirstOrDefault();

        if (type is null)
            return new TextBlock { Text = $"Type Not Found: {name}" };

        var instance = Bootstrapper.Services.GetRequiredService(type);
        if (instance is Control control)
        {
            SetupLifecycleHooks(control, param);
            return control;
        }

        return new TextBlock { Text = $"Create Instance Failed: {type.FullName}" };
    }

    public void SetupLifecycleHooks(Control control, object dataContext)
    {
        control.DataContext = dataContext;

        if (control is Window window)
        {
            window.Opened += static (sender, args) =>
            {
                var control = (Control)sender!;
                if (control.DataContext is ObservableRecipient recipient)
                {
                    recipient.IsActive = true;
                }
                else if (control.DataContext is IActivatable activatable)
                {
                    activatable.IsActive = true;
                }
            };
        }

        control.Loaded += static (sender, args) =>
        {
            var control = (Control)sender!;
            if (control.DataContext is ObservableRecipient recipient)
            {
                recipient.IsActive = true;
            }
            else if (control.DataContext is IActivatable activatable)
            {
                activatable.IsActive = true;
            }
        };

        control.Unloaded += static (sender, args) =>
        {
            var control = (Control)sender!;
            if (control.DataContext is ObservableRecipient recipient)
            {
                recipient.IsActive = false;
            }
            else if (control.DataContext is IActivatable activatable)
            {
                activatable.IsActive = false;
            }
        };

        if (dataContext is IViewAware viewAware)
        {
            viewAware.AttachView(control);
        }
    }
}
