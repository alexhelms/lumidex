using Lumidex.Core.Data;
using Lumidex.Core.Pipelines;
using Microsoft.EntityFrameworkCore;
using System.IO.Abstractions;
using Lumidex.Tests.Fixtures;

namespace Lumidex.Tests;

public class LibraryIngestPipelineTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _dbFixture;
    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly IDbContextFactory<LumidexDbContext> _contextFactory;

    public LibraryIngestPipelineTests(DatabaseFixture dbFixture)
    {
        _dbFixture = dbFixture;
        _contextFactory = new TestDbContextFactory(dbFixture.DatabaseFilename);
    }

    // Regression test for the cross-library dedup bug in upstream issue #66.
    //
    // Reporter scenario: the same physical XISF file was placed in two
    // separate library folders. Scanning the second library produced a
    // confusing "1 Added, 1 Skipped" status — which looks like something
    // went wrong even though the file was, in fact, correctly added to
    // the second library.
    //
    // Root cause: block3GetOrCreateEntity's dedup query searched ImageFiles
    // across ALL libraries by HeaderHash. When the second library was
    // scanned, the query matched the row the first library had created,
    // emitted a spurious "Skipped" status, then still added a fresh row
    // to the second library because the (hash, path) pair didn't match.
    //
    // Fix: scope the dedup query to the library currently being scanned.
    // Libraries are logically independent; a hash match in another
    // library isn't a duplicate from the scanning library's perspective.
    [Fact]
    public async Task SameFileInTwoLibraries_SecondLibraryScan_DoesNotEmitMisleadingSkipped()
    {
        using var xisf = new XisfFixture();
        var generated = xisf.GenerateXisfFile(
            new XisfHeaderContent("OBJECT", "Test-Target"),
            new XisfHeaderContent("EXPOSURE", "60.0"));

        var lib1Dir = Path.Combine(Path.GetTempPath(), $"lumidex-test-lib1-{Guid.NewGuid():N}");
        var lib2Dir = Path.Combine(Path.GetTempPath(), $"lumidex-test-lib2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(lib1Dir);
        Directory.CreateDirectory(lib2Dir);

        try
        {
            File.Copy(generated.FullName, Path.Combine(lib1Dir, "shared.xisf"));
            File.Copy(generated.FullName, Path.Combine(lib2Dir, "shared.xisf"));

            var library1 = new Library { Name = "Lib1", Path = lib1Dir };
            var library2 = new Library { Name = "Lib2", Path = lib2Dir };
            _dbFixture.DbContext.Libraries.AddRange(library1, library2);
            _dbFixture.DbContext.SaveChanges();

            var pipeline = new LibraryIngestPipeline(_fileSystem, _contextFactory);

            // First library — fresh DB, should add cleanly with no skips.
            await pipeline.ProcessAsync(library1, forceFullScan: true);
            pipeline.Added.Count.Should().Be(1);
            pipeline.Skipped.Count.Should().Be(0);

            // Second library — this is the bug scenario. The fix should produce
            // a clean "1 Added, 0 Skipped" because the dedup now scopes to
            // library2's rows only, and library2 has no matching hash yet.
            await pipeline.ProcessAsync(library2, forceFullScan: true);
            pipeline.Added.Count.Should().Be(1);
            pipeline.Skipped.Count.Should().Be(0,
                "upstream #66: the pre-fix behavior reported 1 Skipped here because the "
                + "dedup query matched library1's row across library boundaries");

            // Both libraries should own their own independent row.
            using var verify = _contextFactory.CreateDbContext();
            var files = verify.ImageFiles.ToList();
            files.Should().HaveCount(2);
            files.Select(f => f.LibraryId).Should().BeEquivalentTo(new[] { library1.Id, library2.Id });
        }
        finally
        {
            if (Directory.Exists(lib1Dir)) Directory.Delete(lib1Dir, recursive: true);
            if (Directory.Exists(lib2Dir)) Directory.Delete(lib2Dir, recursive: true);
        }
    }
}
