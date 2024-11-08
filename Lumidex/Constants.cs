using System.Runtime.InteropServices;

namespace Lumidex;

public static class Constants
{
    public static string OpenInExplorer
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "Open in Finder";
            }

            return "Open in Explorer";
        }
    }
}
