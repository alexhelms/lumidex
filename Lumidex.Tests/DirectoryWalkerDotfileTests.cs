using Lumidex.Core.IO;
using System.IO.Abstractions;

namespace Lumidex.Tests;

// Uses a real FileSystem with temp directories, matching the existing
// DirectoryWalker tests — the walk only reads directory/file names here, but a
// real tree keeps the test consistent with the rest of the walker coverage.
public class DirectoryWalkerDotfileTests : IDisposable
{
    private readonly string _libDir;
    private readonly IFileSystem _fileSystem = new FileSystem();

    public DirectoryWalkerDotfileTests()
    {
        _libDir = Path.Combine(Path.GetTempPath(), $"lumidex-test-dotfiles-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_libDir);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_libDir))
            Directory.Delete(_libDir, recursive: true);
    }

    // POSIX hidden directories (.git, .cache, .Trash-*, ...) hold no library
    // images and can be large. The walker must not descend into them.
    // Pre-fix: only "$"-prefixed dirs are skipped, so the file under .git is
    // walked and yielded.
    [Test]
    public async Task Walk_DoesNotDescendIntoDotPrefixedSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_libDir, ".git"));
        File.WriteAllText(Path.Combine(_libDir, ".git", "hidden.fits"), "");
        Directory.CreateDirectory(Path.Combine(_libDir, "normal"));
        File.WriteAllText(Path.Combine(_libDir, "normal", "visible.fits"), "");

        var yielded = DirectoryWalker.Walk(_fileSystem, _libDir).Select(f => f.Name).ToList();

        await Assert.That(yielded).HasSingleItem(x => x == "visible.fits");
    }

    // The skip applies to descendants discovered during the walk, never to the
    // explicitly-chosen root. A library rooted at a dot-directory (e.g.
    // ~/.astrophotos) must still be scanned.
    [Test]
    public async Task Walk_DoesNotSkipDotPrefixedRoot()
    {
        var dotRoot = Path.Combine(_libDir, ".astrophotos");
        Directory.CreateDirectory(dotRoot);
        File.WriteAllText(Path.Combine(dotRoot, "image.fits"), "");

        var yielded = DirectoryWalker.Walk(_fileSystem, dotRoot).Select(f => f.Name).ToList();

        await Assert.That(yielded).HasSingleItem(x => x == "image.fits");
    }
}
