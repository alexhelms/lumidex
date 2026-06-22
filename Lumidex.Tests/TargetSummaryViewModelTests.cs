using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Features.TargetSummary;
using Lumidex.Services;
using Lumidex.Tests.Fixtures;

namespace Lumidex.Tests;

public class TargetSummaryViewModelTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fx;
    public TargetSummaryViewModelTests(DatabaseFixture fx)
    {
        _fx = fx;
        using var db = new LumidexDbContext(_fx.Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    public void Dispose() => GC.SuppressFinalize(this);

    private TargetSummaryViewModel BuildViewModel()
    {
        var factory = new TestDbContextFactory(_fx.DatabaseFilename);
        return new TargetSummaryViewModel(
            new TargetResolutionService(factory),
            new TargetSummaryQuery(factory),
            new DialogService());
    }

    // The bar reference (the "share of the largest" denominator) must exclude the synthetic
    // "(Unnamed)" pile, which is often the biggest — otherwise every real target's bar scales
    // against the junk row. Seed an unnamed pile LARGER than the one real target and assert the
    // real target's ReferenceMax is its own hours, not the bigger pile.
    [Fact]
    public async Task Reload_ReferenceMax_ExcludesUnnamedPile()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="r", Path="/r", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="u", Path="/u", ObjectName=null,   Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=7200, LibraryId=1 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();

        var m31 = vm.Targets.Single(t => t.CanonicalName == "M 31");
        m31.ReferenceMax.Should().BeApproximately(1.0, 1e-6);   // its own 1h, not the unnamed 2h
        m31.Remainder.Should().BeApproximately(0, 1e-6);        // it is the largest real target
    }
}
