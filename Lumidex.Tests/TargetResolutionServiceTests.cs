using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Tests.Fixtures;

namespace Lumidex.Tests;

// Each test needs an isolated DB; the ctor wipes and recreates the schema so seeded rows from one
// test never leak into the next. The fixture itself is created once per class by IClassFixture.
public class TargetResolutionServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fx;
    public TargetResolutionServiceTests(DatabaseFixture fx)
    {
        _fx = fx;
        using var db = new LumidexDbContext(_fx.Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    public void Dispose() => GC.SuppressFinalize(this);

    private TargetResolutionService Service() => new(new TestDbContextFactory(_fx.DatabaseFilename));

    [Fact]
    public void EnsureTargetsResolved_CreatesOneTargetPerDistinctName_AndIsIdempotent()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "a", Path = "/a", ObjectName = "M 31",  Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "b", Path = "/b", ObjectName = "M 31",  Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "c", Path = "/c", ObjectName = "M 104", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "d", Path = "/d", ObjectName = null,    Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        var svc = Service();
        svc.EnsureTargetsResolved();
        svc.EnsureTargetsResolved();   // idempotent

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Select(t => t.CanonicalName).OrderBy(x => x)
            .Should().BeEquivalentTo(new[] { "M 104", "M 31" });   // null ObjectName produces no target
        verify.TargetNameMaps.Should().HaveCount(2);
    }

    // A whitespace-only ObjectName ("   ", "\t") is treated as empty: no target, no map. SQLite's
    // TRIM strips only spaces, so the tab case proves the filter runs client-side.
    [Fact]
    public void EnsureTargetsResolved_WhitespaceObjectName_CreatesNoTarget()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "sp",   Path = "/sp",   ObjectName = "   ",  Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "tab",  Path = "/tab",  ObjectName = "\t",   Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "real", Path = "/real", ObjectName = "M 31", Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        Service().EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Select(t => t.CanonicalName).Should().BeEquivalentTo(new[] { "M 31" });
        verify.TargetNameMaps.Should().ContainSingle();
    }

    // Case-variant ObjectNames ("M 31" / "m 31") collapse to ONE target — the NOCASE map.
    [Fact]
    public void EnsureTargetsResolved_CaseVariantObjectNames_CollapseToOneTarget()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "u", Path = "/u", ObjectName = "M 31", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "l", Path = "/l", ObjectName = "m 31", Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        Service().EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();
        verify.TargetNameMaps.Should().ContainSingle();
    }

    // FITS space-pads OBJECT, so "M 31" and "M 31 " arrive as distinct strings; both trim to one
    // identity and collapse to ONE target + ONE map rather than splitting integration across two.
    [Fact]
    public void EnsureTargetsResolved_WhitespacePaddedObjectNames_CollapseToOneTarget()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "p", Path = "/p", ObjectName = "M 31",  Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "t", Path = "/t", ObjectName = "M 31 ", Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        Service().EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();
        verify.TargetNameMaps.Should().ContainSingle();
    }
}
