using Lumidex.Core.IO;
using Serilog;
using System.Runtime.InteropServices;

namespace Lumidex.Core;

public static class Bootstrapper
{
    private static string LogPath = Path.Combine(LumidexPaths.Logs, "lumidex.log");

    public static void Start()
    {
        InitializeLogger();
        LogApplicationInfo();
        NativeLibraryChecks();
    }

    public static void Stop()
    {
        // Empty for now
    }

    public static void InitializeLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Async(wt => wt.Debug())
            .WriteTo.Async(wt => wt.Console())
            .WriteTo.Async(wt => wt.File(LogPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7))
            .Enrich.FromLogContext()
            .CreateLogger();
    }

    private static void LogApplicationInfo()
    {
        var launchTimeUtc = DateTime.UtcNow;

        Log.Information("Welcome to Lumidex {Version} {Architecture}", LumidexUtil.InformationalVersion, LumidexUtil.ProcessArchitecture);
        Log.Information("Launched at {TimestampUtc:s} UTC ({TimestampLocal:s} Local)", launchTimeUtc, launchTimeUtc.ToLocalTime());
        Log.Information("{OS} {Architecture}", LumidexUtil.OSDescription, LumidexUtil.OSArchitecture);
        Log.Information("{Dotnet} {RuntimeIdentifier}", RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        Log.Information("Logs located at {Path}", LogPath);
    }

    private static void NativeLibraryChecks()
    {
        // Invoking this calls the static ctor which checks cfitsio for reentrancy flag.
        _ = FitsFile.Native.FitsIsReentrant();
    }
}
