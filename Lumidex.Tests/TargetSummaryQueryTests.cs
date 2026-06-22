using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Tests.Fixtures;

namespace Lumidex.Tests;

public class TargetSummaryQueryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fx;
    public TargetSummaryQueryTests(DatabaseFixture fx)
    {
        _fx = fx;
        using var db = new LumidexDbContext(_fx.Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    public void Dispose() => GC.SuppressFinalize(this);

    private TargetSummaryQuery Query() => new(new TestDbContextFactory(_fx.DatabaseFilename));

    // Light frames roll up target -> scope -> filter with hours summed at each level.
    [Fact]
    public void GetTargetSummary_RollsUpHoursByScopeAndFilter()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=3*3600, LibraryId=1 },
                new ImageFile { HeaderHash="c", Path="/c", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="OIII", Exposure=1*3600, LibraryId=1 });
            db.SaveChanges();
        }

        var target = Query().GetTargetSummary().Single(r => r.CanonicalName == "M 31");
        target.Hours.Should().BeApproximately(6, 1e-6);                       // 2 + 3 + 1

        var scope = target.Scopes.Should().ContainSingle().Subject;
        scope.Scope.Should().Be("T20");
        scope.Hours.Should().BeApproximately(6, 1e-6);
        scope.Filters.Single(f => f.Filter == "Ha").Hours.Should().BeApproximately(5, 1e-6);    // 2 + 3
        scope.Filters.Single(f => f.Filter == "OIII").Hours.Should().BeApproximately(1, 1e-6);
    }

    // Date extents span all the frames of a group, ignoring null observation times.
    [Fact]
    public void GetTargetSummary_DateExtents_SpanFrames()
    {
        var d1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, ObservationTimestampUtc=d2, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, ObservationTimestampUtc=d1, LibraryId=1 });
            db.SaveChanges();
        }

        var row = Query().GetTargetSummary().Single();
        row.First.Should().Be(d1);
        row.Last.Should().Be(d2);
    }

    // Light frames with no usable ObjectName roll up under the synthetic "(Unnamed)" row.
    [Fact]
    public void GetTargetSummary_UnnamedFrames_RollUpUnderUnnamedRow()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.Add(new ImageFile { HeaderHash="u", Path="/u", ObjectName=null, Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var rows = Query().GetTargetSummary();
        var unnamed = rows.Should().ContainSingle().Subject;
        unnamed.TargetId.Should().Be(0);
        unnamed.CanonicalName.Should().Be("(Unnamed)");
        unnamed.Hours.Should().BeApproximately(1, 1e-6);
    }
}
