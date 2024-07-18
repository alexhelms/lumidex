namespace Lumidex.Core.IO;

public static class LumidexPaths
{
    public static string AppData
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "Lumidex");
        }
    }

    public static string Logs => Path.Combine(AppData, "Logs");

    public static string DefaultLibrary
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lumidex Library");
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "lumidex");
            throw new PlatformNotSupportedException();
        }
    }
}
