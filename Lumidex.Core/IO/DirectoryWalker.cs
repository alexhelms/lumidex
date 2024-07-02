using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace Lumidex.Core.IO;

public static class DirectoryWalker
{
    public static readonly string[] SupportedExtensions = ["fit", "fits", "xisf"];

    public static IEnumerable<IFileInfo> Walk(string rootDir)
        => Walk(new FileSystem(), rootDir, SupportedExtensions);

    public static IEnumerable<IFileInfo> Walk(string rootDir, string[] extensions)
        => Walk(new FileSystem(), rootDir, extensions);

    public static IEnumerable<IFileInfo> Walk(IFileSystem fs, string rootDir)
        => Walk(fs, rootDir, SupportedExtensions);

    public static IEnumerable<IFileInfo> Walk(IFileSystem fs, string rootDir, string[] extensions)
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
            var files = currentDir.EnumerateFiles()
                .Where(fileInfo => pattern.IsMatch(fileInfo.Extension));
            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var dir in currentDir.EnumerateDirectories())
            {
                stack.Push(dir);
            }
        }
    }
}
