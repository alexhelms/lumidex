using System.Reflection;
using System.Runtime.InteropServices;

namespace Lumidex.Core;

public static class NativeLoader
{
    static NativeLoader()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, AssemblyResolver);
    }

    internal static string GetLibraryPath()
    {
        string platform;

        if (OperatingSystem.IsWindows())
        {
            platform = "windows";
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = "linux";
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = "macos";
        }
        else
            throw new PlatformNotSupportedException();

        return Path.Join("lib", platform);
    }

    private static string GetLibraryPath(string libraryName)
    {
        string prefix;

        if (OperatingSystem.IsWindows())
        {
            prefix = string.Empty;
        }
        else if (OperatingSystem.IsLinux())
        {
            prefix = "lib";
        }
        else if (OperatingSystem.IsMacOS())
        {
            prefix = "lib";
        }
        else
            throw new PlatformNotSupportedException();

        var basePath = GetLibraryPath();
        return Path.Join(basePath, prefix + libraryName);
    }

    private static IntPtr AssemblyResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        return NativeLibrary.Load(
            GetLibraryPath(libraryName),
            typeof(NativeLoader).Assembly,
            searchPath);
    }
}
