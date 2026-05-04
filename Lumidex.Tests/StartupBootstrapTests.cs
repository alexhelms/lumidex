using Lumidex.Core;
using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;

namespace Lumidex.Tests;

// Regression coverage for upstream issue #52: "Lumidex crashes on start if it
// can't create the default library." The reporter's stack trace shows
// `UseLumidexCore` calling `IDirectory.CreateDirectory(LumidexPaths.DefaultLibrary)`
// and bubbling out a FileNotFoundException because the parent path was
// unavailable. Pre-fix: app crashes at every launch (the Library row never
// persists, so the next launch re-enters the same crash path). Post-fix:
// expected I/O failures are caught, a warning is logged, and the app starts
// in a no-library state where the user can add a library manually.
//
// Uses a real on-disk SQLite file because UseLumidexCore runs migrations,
// and EF migrations don't compose cleanly with the in-memory provider.
public class StartupBootstrapTests : IDisposable
{
    private readonly string _databaseFilename;

    public StartupBootstrapTests()
    {
        // Fresh DB per test — UseLumidexCore expects a virgin schema so it can
        // run migrations and seed AppSettings/Libraries. Reusing a fixture DB
        // would skip the bootstrap branches we're trying to exercise. xUnit
        // creates a new test class instance per test method, so each [Fact]
        // and each [InlineData] of a [Theory] gets its own DB filename.
        var tempFilename = $"lumidex-bootstrap-{Path.GetFileName(Path.GetTempFileName())}.db";
        _databaseFilename = Path.Combine(Path.GetTempPath(), "lumidex", tempFilename);
        Directory.CreateDirectory(Path.GetDirectoryName(_databaseFilename)!);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (File.Exists(_databaseFilename))
            File.Delete(_databaseFilename);
    }

    // Build a minimal IServiceProvider that UseLumidexCore can consume.
    // Registers the supplied IFileSystem (mock for the failing path, real
    // for the happy path) and a scoped LumidexDbContext pointing at this
    // test's temp DB file. We do not call AddLumidexCore because that
    // registers the production DbContext factory pointed at the user's
    // real AppData directory.
    private IServiceProvider BuildServiceProvider(IFileSystem fileSystem)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fileSystem);
        services.AddDbContext<LumidexDbContext>(options =>
            options.UseSqlite($"Data Source={_databaseFilename}"));
        return services.BuildServiceProvider();
    }

    // Open a fresh LumidexDbContext on the same SQLite file the test bootstrap
    // wrote to, so we can assert on what was (or wasn't) persisted. We don't
    // reuse the IServiceProvider's scoped context because UseLumidexCore
    // creates and disposes its own scope before returning.
    private LumidexDbContext OpenDbContext()
    {
        return new LumidexDbContext(
            new DbContextOptionsBuilder<LumidexDbContext>()
                .UseSqlite($"Data Source={_databaseFilename}")
                .Options);
    }

    // Parameterized regression test for #52, covering every exception type the
    // catch filter is designed to handle. The reporter's actual stack trace
    // was FileNotFoundException (parent path missing on Windows); the other
    // shapes can plausibly arise from readonly home / XDG-strict / NTFS
    // permissions / pathological path lengths. Every case must be caught:
    // the app must start, and no Library row should be persisted (a row
    // pointing at a path that couldn't be created is worse UX than no row).
    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    [InlineData(typeof(PathTooLongException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void UseLumidexCore_DefaultLibraryCreationFailsWithExpectedException_DoesNotThrow_AndNoLibraryRowAdded(Type exceptionType)
    {
        // Activator.CreateInstance constructs the exception with a single
        // string-arg constructor, which all five types support.
        var thrown = (Exception)Activator.CreateInstance(exceptionType, "simulated bootstrap failure")!;

        var mockDir = new Mock<IDirectory>();
        mockDir.Setup(d => d.CreateDirectory(It.IsAny<string>())).Throws(thrown);
        var mockFs = new Mock<IFileSystem>();
        mockFs.Setup(f => f.Directory).Returns(mockDir.Object);

        var provider = BuildServiceProvider(mockFs.Object);

        var act = () => provider.UseLumidexCore();
        act.Should().NotThrow($"{exceptionType.Name} is an expected I/O failure that the catch filter must handle without crashing startup");

        using var dbContext = OpenDbContext();
        dbContext.Libraries.Count().Should().Be(0,
            "no Library row should be added when its directory couldn't be created");
    }

    // Sanity check on the happy path: when CreateDirectory succeeds, the
    // Library row IS added. Guards against the fix accidentally short-circuiting
    // the success case.
    [Fact]
    public void UseLumidexCore_DefaultLibraryCreationSucceeds_AddsLibraryRow()
    {
        var mockDir = new Mock<IDirectory>();
        // Default mock behavior on a void method is "do nothing" — equivalent to
        // CreateDirectory succeeding without actually creating anything on disk.
        var mockFs = new Mock<IFileSystem>();
        mockFs.Setup(f => f.Directory).Returns(mockDir.Object);

        var provider = BuildServiceProvider(mockFs.Object);

        provider.UseLumidexCore();

        using var dbContext = OpenDbContext();
        dbContext.Libraries.Should().HaveCount(1);
        dbContext.Libraries.Single().Name.Should().Be("Default");
    }

    // Negative test: the catch filter is intentionally narrow. Exceptions
    // outside the expected I/O failure set (e.g. an InvalidOperationException
    // from a programming bug) should still propagate so they're loud, not
    // silently swallowed as "user environment issue." This guards against a
    // future "let's just catch Exception" simplification that would mask
    // genuine bugs.
    [Fact]
    public void UseLumidexCore_DefaultLibraryCreationFailsWithUnexpectedException_Propagates()
    {
        var mockDir = new Mock<IDirectory>();
        mockDir.Setup(d => d.CreateDirectory(It.IsAny<string>()))
            .Throws(new InvalidOperationException("simulated programming error"));
        var mockFs = new Mock<IFileSystem>();
        mockFs.Setup(f => f.Directory).Returns(mockDir.Object);

        var provider = BuildServiceProvider(mockFs.Object);

        var act = () => provider.UseLumidexCore();
        act.Should().Throw<InvalidOperationException>(
            "the catch filter is intentionally narrow — non-I/O exceptions must still propagate");
    }
}
