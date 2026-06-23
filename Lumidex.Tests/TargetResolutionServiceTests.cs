using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests;

// Each test needs an isolated DB. The shared DatabaseFixture file is reused, but
// the ctor wipes and recreates the schema so seeded rows from one test never leak
// into the next. (The fixture itself is created once per class by IClassFixture.)
//
// Covers name resolution + auto-consolidation (the destructive AbsorbInto path that stays for
// formatting-variant folds). The user-facing merge surface (now non-destructive records + undo)
// lives in MergeServiceTests; read-time folding lives in TargetGoalQueryTests.
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

    [Fact]
    public void Target_And_NameMap_Persist_And_Relate()
    {
        using var db = new LumidexDbContext(_fx.Options);
        var target = new Target { CanonicalName = "Andromeda Galaxy" };
        db.Targets.Add(target);
        db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = target });
        db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Andromeda", Target = target });
        db.SaveChanges();

        var loaded = db.Targets.Include(t => t.NameMaps).Single();
        loaded.NameMaps.Should().HaveCount(2);
        loaded.CanonicalName.Should().Be("Andromeda Galaxy");
    }

    [Fact]
    public void EnsureTargetsResolved_CreatesOneTargetPerDistinctName_AndIsIdempotent()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            // ImageFile.LibraryId is a required FK; seed the Library row it points at.
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "a", Path = "/a", ObjectName = "M 31", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "b", Path = "/b", ObjectName = "M 31", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "c", Path = "/c", ObjectName = "M 104", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "d", Path = "/d", ObjectName = null,    Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();
        svc.EnsureTargetsResolved(); // idempotent

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Select(t => t.CanonicalName).OrderBy(x => x)
            .Should().BeEquivalentTo(new[] { "M 104", "M 31" });   // null ObjectName produces no map
        verify.TargetNameMaps.Should().HaveCount(2);
    }

    // A whitespace-only ObjectName ("   ", "\t") must be treated as empty: it spawns
    // no Target and no map (the merge side already rejects whitespace via
    // ThrowIfNullOrWhiteSpace — resolution must be symmetric). SQLite's TRIM only
    // strips spaces, so the tab case proves the filter runs client-side.
    [Fact]
    public void EnsureTargetsResolved_WhitespaceObjectName_CreatesNoTarget()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash = "sp", Path = "/sp", ObjectName = "   ", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "tab", Path = "/tab", ObjectName = "\t", Type = ImageType.Light, LibraryId = 1 },
                new ImageFile { HeaderHash = "real", Path = "/real", ObjectName = "M 31", Type = ImageType.Light, LibraryId = 1 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Select(t => t.CanonicalName).Should().BeEquivalentTo(new[] { "M 31" }); // whitespace produces no junk target
        verify.TargetNameMaps.Should().ContainSingle();
    }

    // Case-variant ObjectNames ("M 31" / "m 31") must collapse to ONE Target — the
    // NOCASE promise. Two layers enforce it: ImageFile.ObjectName is TEXT COLLATE
    // NOCASE so the DISTINCT scan already folds the variants, and the OrdinalIgnoreCase
    // HashSet folds again as a backstop. This asserts the observable contract (one
    // target, one map) rather than the mechanism, so it holds if either layer changes.
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

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();          // case variants do not spawn two targets
        verify.TargetNameMaps.Should().ContainSingle();
    }

    // FITS headers often space-pad OBJECT, so "M 31" and "M 31 " arrive as distinct
    // strings and would mint two Targets, splitting integration across two rows. Both
    // must trim to the same identity and collapse to ONE Target + ONE map.
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

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();          // trailing-space twin does not spawn a second target
        verify.TargetNameMaps.Should().ContainSingle();
    }

    // ConsolidateFormattingVariants runs automatically inside EnsureTargetsResolved on
    // every tab load — so a per-scope goal on an absorbed variant is the data-loss path
    // that fires with NO user action. The goal on the absorbed spelling must follow to the
    // survivor.
    [Fact]
    public void EnsureTargetsResolved_Consolidation_PreservesPerFilterGoals()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var big = new Target { CanonicalName = "M101" };           // dominant spelling -> survivor
            var small = new Target { CanonicalName = "M 101" };
            db.Targets.AddRange(big, small);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M101", Target = big });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 101", Target = small });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="1", Path="/1", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="2", Path="/2", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="3", Path="/3", ObjectName="M 101", Type=ImageType.Light, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = small, Scope = "T20", Filter = "L", GoalHours = 15 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();                           // the two variants consolidated
        var goal = verify.TargetFilterGoals.Should().ContainSingle().Subject;
        goal.GoalHours.Should().Be(15);                                    // absorbed variant's goal survived
        goal.TargetId.Should().Be(verify.Targets.Single().Id);            // ...repointed onto the survivor
    }

    // AbsorbInto's collision de-dupe still runs on the consolidation path (its only remaining
    // caller). Two formatting-variant targets each set a goal for the same physical scope spelled
    // in different case ("T20"/"t20") — TargetFilterGoal.Scope is BINARY but its source is NOCASE,
    // so they are one logical scope but two unique-index keys. Consolidation must collapse them to
    // one row (larger goal wins) without aborting on the unique index.
    [Fact]
    public void EnsureTargetsResolved_Consolidation_CollidingCaseVariantGoals_KeepsLarger()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var big = new Target { CanonicalName = "M101" };           // dominant -> survivor
            var small = new Target { CanonicalName = "M 101" };
            db.Targets.AddRange(big, small);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M101", Target = big });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 101", Target = small });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="1", Path="/1", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="2", Path="/2", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="3", Path="/3", ObjectName="M 101", Type=ImageType.Light, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = big,   Scope = "T20", Filter = "L", GoalHours = 10 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = small, Scope = "t20", Filter = "L", GoalHours = 40 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        var resolve = () => svc.EnsureTargetsResolved();

        resolve.Should().NotThrow();                                        // no unique-index abort mid-consolidate
        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetFilterGoals.Should().ContainSingle();                  // the case-variants collapse to one row
        verify.TargetFilterGoals.Single().GoalHours.Should().Be(40);        // larger goal wins
    }

    // Two absorbed variants share a scope goal and the survivor has none — exercises AbsorbInto's
    // "keeper = group.First()" branch (no survivor row to prefer). Three same-key spellings fold to
    // one target; the two colliding goals collapse to one, larger wins.
    [Fact]
    public void EnsureTargetsResolved_Consolidation_TwoAbsorbedShareScope_KeepsLarger()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var big = new Target { CanonicalName = "M101" };          // dominant -> survivor, no goal
            var a = new Target { CanonicalName = "M 101" };
            var b = new Target { CanonicalName = "M-101" };           // hyphen folds to the same key
            db.Targets.AddRange(big, a, b);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M101", Target = big });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 101", Target = a });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M-101", Target = b });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="1", Path="/1", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="2", Path="/2", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="3", Path="/3", ObjectName="M 101", Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="4", Path="/4", ObjectName="M-101", Type=ImageType.Light, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = a, Scope = "T20", Filter = "L", GoalHours = 8 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = b, Scope = "T20", Filter = "L", GoalHours = 22 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        var resolve = () => svc.EnsureTargetsResolved();

        resolve.Should().NotThrow();                                        // both absorbed goals fold to one, no abort
        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();
        verify.TargetFilterGoals.Should().ContainSingle();
        verify.TargetFilterGoals.Single().GoalHours.Should().Be(22);        // larger of the two absorbed goals
    }

    // Light dedupe: formatting variants of a name in the raw data (whitespace /
    // punctuation / case) map to ONE target each, not several.
    [Fact]
    public void EnsureTargetsResolved_FormattingVariants_CollapseToOneTarget()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="NGC 2070",      Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Ngc2070",       Type=ImageType.Light, LibraryId=1 }, // whitespace+case twin
                new ImageFile { HeaderHash="c", Path="/c", ObjectName="Bode's Galaxy", Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="d", Path="/d", ObjectName="Bodes Galaxy",  Type=ImageType.Light, LibraryId=1 }); // apostrophe twin
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().HaveCount(2);                          // NGC 2070 family + Bode's family
        verify.TargetNameMaps.Should().HaveCount(4);                  // every raw spelling still maps
        verify.TargetNameMaps.Select(m => m.TargetId).Distinct().Should().HaveCount(2);
    }

    // Existing fragmentation (separate targets from a prior 1:1 run) is consolidated on
    // the next resolve: formatting-variant targets collapse into the most-imaged one.
    [Fact]
    public void EnsureTargetsResolved_ConsolidatesExistingVariantTargets_ByDominantSpelling()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var big = new Target { CanonicalName = "M101" };           // dominant spelling (more frames)
            var small = new Target { CanonicalName = "M 101" };
            db.Targets.AddRange(big, small);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M101", Target = big });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 101", Target = small });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="1", Path="/1", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="2", Path="/2", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="3", Path="/3", ObjectName="M101",  Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="4", Path="/4", ObjectName="M 101", Type=ImageType.Light, LibraryId=1 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().ContainSingle();                      // the two variant targets merged
        verify.Targets.Single().CanonicalName.Should().Be("M101");    // survivor = dominant spelling
        verify.TargetNameMaps.Should().HaveCount(2);                  // both raw spellings still map...
        verify.TargetNameMaps.Select(m => m.TargetId).Distinct().Should().ContainSingle(); // ...onto one target
    }

    // The Phase-B boundary: names that differ by real WORDS are NOT merged by light
    // dedupe (no catalog knowledge). "Tarantula Nebula" vs "NGC 2070", and "Horsehead"
    // vs "Horsehead Nebula", each stay separate until catalog resolution lands.
    [Fact]
    public void EnsureTargetsResolved_DifferentWordNames_StaySeparate()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Tarantula Nebula", Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="NGC 2070",         Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="c", Path="/c", ObjectName="Horsehead",        Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="d", Path="/d", ObjectName="Horsehead Nebula", Type=ImageType.Light, LibraryId=1 });
            db.SaveChanges();
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.EnsureTargetsResolved();

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Select(t => t.CanonicalName).OrderBy(x => x)
            .Should().BeEquivalentTo(new[] { "Horsehead", "Horsehead Nebula", "NGC 2070", "Tarantula Nebula" });
    }

    // A user TargetMerge whose SURVIVOR is later folded away by auto-consolidation must follow the
    // consolidation (repointed onto the new survivor), not silently un-merge into a ghost record. The
    // user merge's survivor ("M 101") is a formatting variant that consolidation folds into the
    // dominant "M101"; the merge record must repoint from the variant onto "M101".
    [Fact]
    public void EnsureTargetsResolved_ConsolidationDeletesMergeSurvivor_RepointsMergeRecord()
    {
        int dominantId, variantId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var dominant = new Target { CanonicalName = "M101" };      // 3 frames -> consolidation survivor
            var variant = new Target { CanonicalName = "M 101" };      // 1 frame -> folded away
            var absorbed = new Target { CanonicalName = "Pinwheel" };  // cross-key; user-merged into the variant
            db.Targets.AddRange(dominant, variant, absorbed);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M101", Target = dominant });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 101", Target = variant });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Pinwheel", Target = absorbed });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="1", Path="/1", ObjectName="M101",     Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="2", Path="/2", ObjectName="M101",     Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="3", Path="/3", ObjectName="M101",     Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="4", Path="/4", ObjectName="M 101",    Type=ImageType.Light, LibraryId=1 },
                new ImageFile { HeaderHash="5", Path="/5", ObjectName="Pinwheel", Type=ImageType.Light, LibraryId=1 });
            db.SaveChanges();
            dominantId = dominant.Id; variantId = variant.Id; absorbedId = absorbed.Id;
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.MergeTargets(new[] { variantId, absorbedId }, "M 101");   // Pinwheel -> the variant (survivor)
        svc.EnsureTargetsResolved();                                  // folds the variant into dominant

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Any(t => t.Id == variantId).Should().BeFalse();          // variant consolidated away
        var record = verify.TargetMerges.Should().ContainSingle().Subject;
        record.AbsorbedTargetId.Should().Be(absorbedId);
        record.SurvivorTargetId.Should().Be(dominantId);                        // repointed onto the new survivor
    }
}
