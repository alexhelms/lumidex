using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace Lumidex.Core.IO;

public static class DirectoryWalker
{
    public static readonly string[] SupportedExtensions = ["fit", "fits", "xisf"];

    public static IEnumerable<IFileInfo> Walk(
        IFileSystem fs, 
        string rootDir, 
        DateTime? startDateUtc = null,
        Predicate<IDirectoryInfo>? directoryFilter = null) 
        => Walk(fs, rootDir, SupportedExtensions, startDateUtc, directoryFilter);

    public static IEnumerable<IFileInfo> Walk(
        IFileSystem fs, 
        string rootDir,
        string[] extensions,
        DateTime? startDateUtc = null,
        Predicate<IDirectoryInfo>? directoryFilter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentOutOfRangeException.ThrowIfLessThan(extensions.Length, 1);

        var dirInfo = fs.DirectoryInfo.New(rootDir);
        if (!dirInfo.Exists)
        {
            yield break;
        }

        // $ are windows system folders we can skip
        if (dirInfo.Name.StartsWith('$'))
        {
            yield break;
        }

        var patternString = string.Join('|', extensions.Select(ext => $@"\.{ext}"));
        var pattern = new Regex(patternString, RegexOptions.IgnoreCase);
        var dirStack = new Stack<IDirectoryInfo>();

        dirStack.Push(dirInfo);

        while (dirStack.Count > 0)
        {
            IDirectoryInfo currentDir = dirStack.Pop();

            if (directoryFilter is not null)
            {
                // Apply directory filter
                if (!directoryFilter.Invoke(currentDir))
                    continue;
            }

            IEnumerable<IFileInfo>? files = [];

            try
            {
                files = currentDir
                    .EnumerateFiles()
                    .IgnoreExceptions()
                    .Where(fileInfo => pattern.IsMatch(fileInfo.Extension));
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }

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

            IEnumerable<IDirectoryInfo> directories = [];

            try
            {
                directories = currentDir
                    .EnumerateDirectories()
                    .IgnoreExceptions();
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }

            foreach (var dir in directories)
            {
                // $ are windows system folders we can skip
                if (dir.Name.StartsWith('$'))
                    continue;

                dirStack.Push(dir);
            }
        }
    }
}
