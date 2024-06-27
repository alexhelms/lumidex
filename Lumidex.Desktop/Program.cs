using Projektanker.Icons.Avalonia.FontAwesome;
using Serilog;

namespace Lumidex.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Bootstrapper.Start();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Unhandled application exception");
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
