using System.Diagnostics;
using Velopack;

namespace Lumidex;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnFirstRun(OnFirstRun)
            .Run();
        
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

    private static void OnFirstRun(SemanticVersion version)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var oldDbPath = Path.Combine(localAppData, "Lumidex", "lumidex-data.db");
        var newDbPath = Path.Combine(appData, "Lumidex", "lumidex-data.db");

        try
        {
            // Before Lumidex 2.0, %localappdata% was used. Lumidex 2.0 moved from InnoSetup to Velopack
            // and Velopack deletes %localappdata%/Lumidex on install/update and we're supposed to be
            // using %appdata% instead. On first install of Lumidex 2.0+ move the existing data (if present)
            // to the new location.
            if (File.Exists(oldDbPath))
            {
                Directory.CreateDirectory(Path.Combine(appData, "Lumidex"));        
                File.Copy(oldDbPath, newDbPath, overwrite: true);
                File.Delete(oldDbPath);
            }
        }
        catch { }
    }
}