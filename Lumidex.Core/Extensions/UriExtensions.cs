namespace System;

public static class UriExtensions
{
    public static string ToPath(this Uri uri)
    {
        try
        {
            return Uri.UnescapeDataString(uri.AbsolutePath);
        }
        catch (InvalidOperationException)
        {
            return Uri.UnescapeDataString(uri.ToString());
        }
    }
}
