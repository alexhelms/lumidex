using System.Diagnostics;

namespace Lumidex;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // TODO: Consider a splash screen that is shown immediately and before bootstrap.
        
        try
        {
            Bootstrapper.Start();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {   
            Log.Fatal(e, "Unhandled application exception");
            // TODO: Try to open some kind of message box

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>();

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Disable Avalonia's Linux global-menu (DBus app-menu) export. It targets
            // com.canonical.AppMenu.Registrar, which KDE Plasma 6 neither owns nor makes
            // activatable (it uses org.kde.kappmenu + a gmenu proxy instead), so the
            // export raises an unobserved DBus "ServiceUnknown: The name is not
            // activatable" task that the finalizer would otherwise turn fatal. Our
            // NativeMenu is empty, so nothing is lost by not exporting it. This is an
            // X11-only option, so Windows and macOS are unaffected.
            .With(new X11PlatformOptions { UseDBusMenu = false })
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
    }
}