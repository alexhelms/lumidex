using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Lumidex.Core.Data;
using Lumidex.Features.Main;
using Microsoft.EntityFrameworkCore;
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
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = Bootstrapper.Services;

            var dbContextFactory = services.GetRequiredService<IDbContextFactory<LumidexDbContext>>();
            using var dbContext = dbContextFactory.CreateDbContext();
            var fullScreen = dbContext.AppSettings.FirstOrDefault()?.FullScreen ?? false;

            // MainWindow requires some manual lifecycle wiring.
            // ViewLocator does the rest for all other views.
            var mainWindow = services.GetRequiredService<MainWindow>();
            var mainViewModel = services.GetRequiredService<MainViewModel>();
            ViewLocator.Instance.SetupLifecycleHooks(mainWindow, mainViewModel);

            if (fullScreen)
            {
                mainWindow.WindowState = WindowState.Maximized;
            }

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
