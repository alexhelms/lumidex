using Lumidex.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Lumidex;

public static class Bootstrapper
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static void Start()
    {
        Core.Bootstrapper.InitializeLogger();
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        var services = new ServiceCollection();
        services.AddLumidexCore();
        services.AddLumidexUi();
        Services = services.BuildServiceProvider();
    }

    public static void Stop()
    {
        (Services as ServiceProvider)?.Dispose();
        Log.Information("Lumidex exiting, bye");
        Log.CloseAndFlush();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled appdomain exception");
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.Exception.Handle(ex =>
        {
            Log.Error(ex, "Unobserved task exception");
            return false;
        });
    }
}
