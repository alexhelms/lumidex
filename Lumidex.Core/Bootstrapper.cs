using Lumidex.Core.IO;
using Serilog;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lumidex.Core;

public static class Bootstrapper
{
    private static string LogPath = Path.Combine(LumidexPaths.Logs, "lumidex.log");

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
#pragma warning restore CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    internal static void InitializeModule()
    {
        RuntimeHelpers.RunClassConstructor(typeof(NativeLoader).TypeHandle);
    }

    public static void Start()
    {
        InitializeLogger();
        LogApplicationInfo();
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
        var executingAssembly = Assembly.GetExecutingAssembly();
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(executingAssembly.Location);
        var version = fileVersionInfo.ProductVersion;
        var processArch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var osArch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        Log.Information("Welcome to Lumidex {Version} {Architecture}", version, processArch);
        Log.Information("Launched at {TimestampUtc:s} UTC ({TimestampLocal:s} Local)", launchTimeUtc, launchTimeUtc.ToLocalTime());
        Log.Information("{OS} {Architecture}", RuntimeInformation.OSDescription, osArch);
        Log.Information("{Dotnet} {RuntimeIdentifier}", RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier);
        Log.Information("Logs located at {Path}", LogPath);
        Log.Information(LumidexPaths.DefaultLibrary);
    }
}
