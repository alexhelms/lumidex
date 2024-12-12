using Lumidex.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Lumidex;

public static class Bootstrapper
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static void Start()
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        Core.Bootstrapper.Start();

        var services = new ServiceCollection();
        services.AddLumidexCore();
        services.AddLumidexUi();
        Services = services.BuildServiceProvider();

        Services.UseLumidexCore();
    }

    public static void Stop()
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new ExitingMessage());
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while stopping Lumidex");
        }

        Log.Information("Lumidex exiting, bye");
        (Services as ServiceProvider)?.Dispose();
        Core.Bootstrapper.Stop();
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
