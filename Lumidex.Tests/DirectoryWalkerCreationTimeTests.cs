using Lumidex.Core.IO;

namespace Lumidex.Tests;

// Exercises the quick-scan date filter through a mocked file system, so a file's
// CreationTime can be set independently of its mtime and is returned verbatim on
// every OS. (On a real file, Linux reports min(birthtime, mtime); testing against
// the IFileInfo abstraction keeps the assertions deterministic.)
public class DirectoryWalkerCreationTimeTests
{
    private const string LibDir = "/library";

    private static MockFileSystem FileWith(DateTime lastWriteUtc, DateTime creationUtc)
    {
        var fs = new MockFileSystem();
        fs.AddFile($"{LibDir}/image.fits", new MockFileData("data")
        {
            LastWriteTime = new DateTimeOffset(lastWriteUtc, TimeSpan.Zero),
            CreationTime = new DateTimeOffset(creationUtc, TimeSpan.Zero),
        });
        return fs;
    }

    // The reported #66 case: a file copied in with a preserved (old) mtime but a
    // recent creation time must still be yielded — an mtime-only filter skips it.
    [Test]
    public async Task Walk_PreservedOldMtimeRecentCreation_IsYielded()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var fs = FileWith(lastWriteUtc: start.AddHours(-1), creationUtc: start.AddMinutes(30));

        await Assert.That(DirectoryWalker.Walk(fs, LibDir, start))
            .HasSingleItem(x => x.Name == "image.fits");
    }

    // Both timestamps older than the cutoff: nothing is yielded.
    [Test]
    public async Task Walk_BothTimestampsOld_YieldsNothing()
    {
        var start = DateTime.UtcNow;
        var fs = FileWith(lastWriteUtc: start.AddHours(-2), creationUtc: start.AddHours(-2));

        await Assert.That(DirectoryWalker.Walk(fs, LibDir, start)).IsEmpty();
    }

    // Recent mtime alone qualifies (the fast path), whatever the creation time.
    [Test]
    public async Task Walk_RecentMtime_IsYielded()
    {
        var start = DateTime.UtcNow.AddHours(-1);
        var fs = FileWith(lastWriteUtc: start.AddMinutes(30), creationUtc: start.AddHours(-5));

        await Assert.That(DirectoryWalker.Walk(fs, LibDir, start)).HasSingleItem();
    }

    // A null cutoff disables date filtering entirely.
    [Test]
    public async Task Walk_NullStartDate_YieldsFile()
    {
        var old = DateTime.UtcNow.AddYears(-1);
        var fs = FileWith(lastWriteUtc: old, creationUtc: old);

        await Assert.That(DirectoryWalker.Walk(fs, LibDir, startDateUtc: null)).HasSingleItem();
    }
}
