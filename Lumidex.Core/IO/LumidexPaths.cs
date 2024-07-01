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

    public static string DefaultLibrary => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lumidex Library");
}
