using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace Lumidex.Core.IO;

public static class DirectoryWalker
{
    public static readonly string[] SupportedExtensions = ["fit", "fits", "xisf"];

    public static IEnumerable<IFileInfo> Walk(string rootDir, DateTime? startDateUtc = null)
        => Walk(new FileSystem(), rootDir, SupportedExtensions, startDateUtc);

    public static IEnumerable<IFileInfo> Walk(string rootDir, string[] extensions, DateTime? startDateUtc = null)
        => Walk(new FileSystem(), rootDir, extensions, startDateUtc);

    public static IEnumerable<IFileInfo> Walk(IFileSystem fs, string rootDir, DateTime? startDateUtc = null)
        => Walk(fs, rootDir, SupportedExtensions, startDateUtc);

    public static IEnumerable<IFileInfo> Walk(IFileSystem fs, string rootDir, string[] extensions, DateTime? startDateUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentOutOfRangeException.ThrowIfLessThan(extensions.Length, 1);

        var dirInfo = fs.DirectoryInfo.New(rootDir);
        if (!dirInfo.Exists)
        {
            yield break;
        }

        var patternString = string.Join('|', extensions.Select(ext => $@"\.{ext}"));
        var pattern = new Regex(patternString, RegexOptions.IgnoreCase);
        var stack = new Stack<IDirectoryInfo>();

        stack.Push(dirInfo);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();

            IEnumerable<IFileInfo>? files = [];

            try
            {
                files = currentDir
                    .EnumerateFiles()
                    .Where(fileInfo => pattern.IsMatch(fileInfo.Extension));
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (startDateUtc.HasValue)
                {
                    if (file.LastWriteTimeUtc >= startDateUtc.Value)
                    {
                        yield return file;
                    }
                }
                else
                {
                    yield return file;
                }
            }

            foreach (var dir in currentDir.EnumerateDirectories())
            {
                stack.Push(dir);
            }
        }
    }
}
