using Lumidex.Core.IO;
using Serilog;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Lumidex.Core;

public static class Bootstrapper
{
    private static string LogPath = Path.Combine(LumidexPaths.Logs, "lumidex.log");

    public static void Start()
    {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);

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
        Log.Information("Launched at {TimestampUtc:s} ({TimestampLocal:s})", launchTimeUtc, launchTimeUtc.ToLocalTime());
        Log.Information("{OS} {Architecture}", LumidexUtil.OSDescription, LumidexUtil.OSArchitecture);
        Log.Information("{Dotnet} {RuntimeIdentifier}", RuntimeInformation.FrameworkDescription, LumidexUtil.PortableRuntimeIdentifier);

        Log.Information("Logs located at {Path}", LogPath);
    }

    private static void NativeLibraryChecks()
    {
        // Invoking this calls the static ctor which checks cfitsio for reentrancy flag.
        _ = FitsFile.Native.FitsIsReentrant();
    }

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var path = GetNativeLibraryPath(libraryName);
        if (path is null)
            return IntPtr.Zero;

        NativeLibrary.TryLoad(path, assembly, searchPath, out IntPtr handle);
        return handle;
    }

    // Builds the absolute path to a bundled native library:
    //   <app base>/runtimes/<portable-rid>/native/<prefix><name><ext>
    // Returns null for platforms with no bundled library, leaving resolution to
    // the runtime's default search (which then fails loudly, as before).
    private static string? GetNativeLibraryPath(string libraryName)
    {
        var prefix = string.Empty;
        var extension = ".dll";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            prefix = "lib";
            extension = ".so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            prefix = "lib";
            extension = ".dylib";
        }

        // Resolve the portable RID whose runtimes/<rid>/native/ folder we ship.
        // RuntimeInformation.RuntimeIdentifier is unusable here: on Linux it is
        // the distro-specific RID (e.g. "fedora.44-x64"), which has no matching
        // runtimes/ folder, so the load silently misses. It only happens to line
        // up on Windows ("win-x64") — which is why this bug is Linux-only.
        var rid = GetNativeRuntimeIdentifier();
        if (rid is null)
            return null;

        // Anchor at the app's base directory, not "./". A relative path resolves
        // against the current working directory, which equals the app folder
        // only when launched from it (e.g. `dotnet run`). An installed build
        // started from a .desktop entry or any other CWD would miss the lib.
        return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", $"{prefix}{libraryName}{extension}");
    }

    // Maps OS + process architecture to the portable RID we bundle a native
    // cfitsio for. Returns null for platforms with no bundled library, leaving
    // resolution to the runtime's default search (which then fails loudly with
    // DllNotFoundException, as before).
    private static string? GetNativeRuntimeIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && arch is Architecture.X64)
            return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && arch is Architecture.X64)
            return "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return arch is Architecture.Arm64 ? "osx-arm64" : "osx-x64";

        return null;
    }
}
