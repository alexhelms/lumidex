using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using Lumidex.Features.Main;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex;

public partial class App : Application
{
    public static readonly StyledProperty<bool> IsMacOSProperty =
        AvaloniaProperty.Register<App, bool>(nameof(Lumidex.Core.LumidexUtil.IsMacOS),
            defaultValue: Lumidex.Core.LumidexUtil.IsMacOS,
            defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public override void Initialize()
    {
        this.EnableHotReload();
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Bootstrapper.Services;

            // MainWindow requires some manual lifecycle wiring.
            // ViewLocator does the rest for all other views.
            var mainWindow = services.GetRequiredService<MainWindow>();
            var mainViewModel = services.GetRequiredService<MainViewModel>();
            ViewLocator.Instance.SetupLifecycleHooks(mainWindow, mainViewModel);

            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            desktop.Exit += OnExit;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            throw new NotImplementedException();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        Bootstrapper.Stop();
    }
}
