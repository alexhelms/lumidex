using Projektanker.Icons.Avalonia.FontAwesome;
using Serilog;

namespace Lumidex.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // TODO: Consider a splash screen that is shown immediately and before bootstrap.

        Bootstrapper.Start();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Unhandled application exception");
            // TODO: Try to open some kind of message box
            Log.CloseAndFlush();
            Environment.Exit(1);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();

        return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
    