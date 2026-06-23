using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Tests.Fixtures;

namespace Lumidex.Tests;

public class TargetGoalQueryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fx;
    public TargetGoalQueryTests(DatabaseFixture fx)
    {
        _fx = fx;
        using var db = new LumidexDbContext(_fx.Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    public void Dispose() => GC.SuppressFinalize(this);

    private TargetGoalQuery Query() => new(new TestDbContextFactory(_fx.DatabaseFilename));

    // Dustin's worked example: goals live on filters; scope/target goals are derived sums; an
    // unset filter's goal defaults to its current actual (so it reads as complete and adds its
    // hours to the rolled-up goal). Bode's on Eon70 = Red 30h (goal 30) + Blue 30h (goal 30),
    // plus T68 Color 1h with no goal -> Eon70 goal 60, T68 goal 1, Bode's goal 61.
    [Fact]
    public void GetTargetGoals_RollsUpFilterGoals_UnsetDefaultsToActual()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Bode's" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Bode's", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="r", Path="/r", ObjectName="Bode's", Type=ImageType.Light, TelescopeName="Eon70", FilterName="Red",   Exposure=30*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Bode's", Type=ImageType.Light, TelescopeName="Eon70", FilterName="Blue",  Exposure=30*3600, LibraryId=1 },
                new ImageFile { HeaderHash="c", Path="/c", ObjectName="Bode's", Type=ImageType.Light, TelescopeName="T68",   FilterName="Color", Exposure=1*3600,  LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "Eon70", Filter = "Red",  GoalHours = 30 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "Eon70", Filter = "Blue", GoalHours = 30 });
            db.SaveChanges();
        }

        var bodes = Query().GetTargetGoals().Single(r => r.CanonicalName == "Bode's");

        bodes.Hours.Should().BeApproximately(61, 1e-6);
        bodes.Goal.Should().BeApproximately(61, 1e-6);   // 30 + 30 + (unset Color = its 1h actual)

        var eon70 = bodes.Scopes.Single(s => s.Scope == "Eon70");
        eon70.Hours.Should().BeApproximately(60, 1e-6);
        eon70.Goal.Should().BeApproximately(60, 1e-6);

        var t68 = bodes.Scopes.Single(s => s.Scope == "T68");
        t68.Goal.Should().BeApproximately(1, 1e-6);      // Color unset -> goal = actual

        var color = t68.Filters.Single(f => f.Filter == "Color");
        color.ExplicitGoal.Should().BeNull();
        color.EffectiveGoal.Should().BeApproximately(1, 1e-6);
    }

    // An explicit goal above the acquired hours stays the goal (the filter reads < 100%), and
    // it's the explicit value that rolls up — not the smaller actual.
    [Fact]
    public void GetTargetGoals_ExplicitGoalAboveActual_GoalIsExplicit()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 81" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 81", Target = t });
            db.ImageFiles.Add(new ImageFile { HeaderHash="h", Path="/h", ObjectName="M 81", Type=ImageType.Light, TelescopeName="Eon70", FilterName="Ha", Exposure=10*3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "Eon70", Filter = "Ha", GoalHours = 40 });
            db.SaveChanges();
        }

        var ha = Query().GetTargetGoals().Single().Scopes.Single().Filters.Single();
        ha.Hours.Should().BeApproximately(10, 1e-6);
        ha.ExplicitGoal.Should().Be(40);
        ha.EffectiveGoal.Should().Be(40);                       // explicit wins even though actual is less

        Query().GetTargetGoals().Single().Goal.Should().BeApproximately(40, 1e-6);
    }

    // Date extents span all the frames of a group (the date-sort inputs), ignoring null
    // observation times.
    [Fact]
    public void GetTargetGoals_DateExtents_SpanFrames()
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
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="Eon70", FilterName="L", Exposure=3600, ObservationTimestampUtc=d2, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31", Type=ImageType.Light, TelescopeName="Eon70", FilterName="L", Exposure=3600, ObservationTimestampUtc=d1, LibraryId=1 });
            db.SaveChanges();
        }

        var row = Query().GetTargetGoals().Single();
        row.First.Should().Be(d1);   // earliest frame
        row.Last.Should().Be(d2);    // latest frame
    }

    // Filter synonyms canonicalize before the roll-up, so "H" and "Ha" merge into one filter
    // row with their hours summed.
    [Fact]
    public void GetTargetGoals_CanonicalizesFilterSynonyms()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="Eon70", FilterName="H",  Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31", Type=ImageType.Light, TelescopeName="Eon70", FilterName="Ha", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single().Scopes.Single();
        scope.Filters.Should().ContainSingle();                        // H + Ha merge to one canonical filter
        scope.Filters.Single().Filter.Should().Be("Ha");
        scope.Filters.Single().Hours.Should().BeApproximately(2, 1e-6); // 1h + 1h
    }

    // An active ScopeMerge folds two telescope-name spellings on a target into ONE scope row at read
    // time, hours summed — no physical change to the frames (Undo just removes the record).
    [Fact]
    public void GetTargetGoals_ScopeMerge_FoldsTwoScopesIntoOneRow()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Horsehead" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="iTelescope 75",  FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="iTelescope T75", FilterName="Ha", Exposure=3*3600, LibraryId=1 });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "iTelescope 75", CanonicalName = "iTelescope T75" });
            db.SaveChanges();
        }

        var row = Query().GetTargetGoals().Single(r => r.CanonicalName == "Horsehead");
        var scope = row.Scopes.Should().ContainSingle().Subject;       // the two spellings fold to one scope
        scope.Scope.Should().Be("iTelescope T75");                     // ...under the canonical name
        scope.Hours.Should().BeApproximately(5, 1e-6);                 // 2h + 3h summed
    }

    // An active TargetMerge folds two distinct-name targets into ONE row at read time; the absorbed
    // target's frames roll up under the survivor's canonical name.
    [Fact]
    public void GetTargetGoals_TargetMerge_FoldsTwoTargetsIntoOneRow()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var survivor = new Target { CanonicalName = "Horsehead" };
            var absorbed = new Target { CanonicalName = "Barnard 33" };   // different words -> no auto-consolidate
            db.Targets.AddRange(survivor, absorbed);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = survivor });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Barnard 33", Target = absorbed });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead",  Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Barnard 33", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=3*3600, LibraryId=1 });
            db.SaveChanges();
            db.TargetMerges.Add(new TargetMerge { OperationId = Guid.NewGuid(), SurvivorTargetId = survivor.Id, AbsorbedTargetId = absorbed.Id, SurvivorLabel = "Horsehead", AbsorbedLabel = "Barnard 33" });
            db.SaveChanges();
        }

        var rows = Query().GetTargetGoals().Where(r => r.TargetId != 0).ToList();
        var row = rows.Should().ContainSingle().Subject;               // two targets fold to one row
        row.CanonicalName.Should().Be("Horsehead");                    // ...under the survivor's name
        row.Hours.Should().BeApproximately(5, 1e-6);                   // both targets' frames summed
    }

    // When both merged targets set a goal for the same scope+filter, the rolled-up goal is the
    // larger (the goals dict's GroupBy+Max) — no physical goal merge needed.
    [Fact]
    public void GetTargetGoals_TargetMerge_GoalCollision_PicksMax()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var survivor = new Target { CanonicalName = "Horsehead" };
            var absorbed = new Target { CanonicalName = "Barnard 33" };
            db.Targets.AddRange(survivor, absorbed);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = survivor });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Barnard 33", Target = absorbed });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead",  Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Barnard 33", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=3*3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = survivor, Scope = "T20", Filter = "Ha", GoalHours = 10 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = absorbed, Scope = "T20", Filter = "Ha", GoalHours = 25 });
            db.SaveChanges();
            db.TargetMerges.Add(new TargetMerge { OperationId = Guid.NewGuid(), SurvivorTargetId = survivor.Id, AbsorbedTargetId = absorbed.Id, SurvivorLabel = "Horsehead", AbsorbedLabel = "Barnard 33" });
            db.SaveChanges();
        }

        var filter = Query().GetTargetGoals().Single(r => r.TargetId != 0).Scopes.Single().Filters.Single();
        filter.Hours.Should().BeApproximately(5, 1e-6);                // both targets' Ha summed
        filter.ExplicitGoal.Should().Be(25);                          // the larger of the two colliding goals
    }

    // A TargetMerge whose survivor id no longer exists (auto-consolidation deleted it) is skipped,
    // not crashed — the absorbed target stands on its own.
    [Fact]
    public void GetTargetGoals_TargetMerge_DanglingSurvivor_IsSkipped()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Horsehead" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = t });
            db.ImageFiles.Add(new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=2*3600, LibraryId=1 });
            db.SaveChanges();
            db.TargetMerges.Add(new TargetMerge { OperationId = Guid.NewGuid(), SurvivorTargetId = t.Id + 9999, AbsorbedTargetId = t.Id, SurvivorLabel = "gone", AbsorbedLabel = "Horsehead" });
            db.SaveChanges();
        }

        var query = Query();
        var act = () => query.GetTargetGoals();
        act.Should().NotThrow();                                       // dangling survivor must not crash
        act().Single(r => r.TargetId != 0).CanonicalName.Should().Be("Horsehead"); // absorbed stands alone
    }

    // F5: undo's user-visible effect — a merge folds two targets to one row; undo restores two rows.
    // The record-deletion is unit-tested elsewhere; this pins the headline promise through the query.
    [Fact]
    public void GetTargetGoals_UndoTargetMerge_RestoresSeparateRows()
    {
        int survivorId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var survivor = new Target { CanonicalName = "Horsehead" };
            var absorbed = new Target { CanonicalName = "Barnard 33" };
            db.Targets.AddRange(survivor, absorbed);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = survivor });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Barnard 33", Target = absorbed });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead",  Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Barnard 33", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=3*3600, LibraryId=1 });
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id;
        }

        var svc = new TargetResolutionService(new TestDbContextFactory(_fx.DatabaseFilename));
        svc.MergeTargets(new[] { survivorId, absorbedId }, "Horsehead");
        Query().GetTargetGoals().Count(r => r.TargetId != 0).Should().Be(1);   // folded to one row

        svc.UndoMerge(svc.GetMergeOperations().Single().OperationId);

        var rows = Query().GetTargetGoals().Where(r => r.TargetId != 0).ToList();
        rows.Should().HaveCount(2);                                            // re-separated by undo
        rows.Select(r => r.CanonicalName).Should().BeEquivalentTo(new[] { "Horsehead", "Barnard 33" });
    }

    // F6: a goal stored under an ABSORBED scope name must roll up under the CANONICAL scope row after
    // a scope merge — the goals-dict scope remap. (Its target-side twin is tested above; this side
    // had no coverage, so deleting the remap would otherwise leave every test green.)
    [Fact]
    public void GetTargetGoals_ScopeMerge_GoalUnderAbsorbedScope_RollsUpUnderCanonical()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Horsehead" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="iTelescope 75",  FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="iTelescope T75", FilterName="Ha", Exposure=3*3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "iTelescope 75", Filter = "Ha", GoalHours = 30 }); // under the absorbed spelling
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "iTelescope 75", CanonicalName = "iTelescope T75" });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "Horsehead").Scopes.Single();
        scope.Scope.Should().Be("iTelescope T75");
        scope.Filters.Single(f => f.Filter == "Ha").ExplicitGoal.Should().Be(30);   // goal followed the fold
    }

    // F11b: a transitive scope chain (A->B then B->C, formed by re-merging a survivor) folds all the
    // way to C through the REAL query, not just the resolver unit — three spellings, one scope row.
    [Fact]
    public void GetTargetGoals_TransitiveScopeChain_FoldsToTerminal()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Horsehead" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="A", FilterName="Ha", Exposure=1*3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="B", FilterName="Ha", Exposure=2*3600, LibraryId=1 },
                new ImageFile { HeaderHash="c", Path="/c", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="C", FilterName="Ha", Exposure=4*3600, LibraryId=1 });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "A", CanonicalName = "B" });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "B", CanonicalName = "C" });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "Horsehead").Scopes.Should().ContainSingle().Subject;
        scope.Scope.Should().Be("C");                                  // A and B both chain to C
        scope.Hours.Should().BeApproximately(7, 1e-6);                 // 1 + 2 + 4
    }

    // Resolver totality: a cyclic merge record set (the UI can't create one, but a corrupt DB or
    // an odd consolidation sequence could) must NOT brick the query — the cyclic keys omit from the
    // map, so the scopes render UN-FOLDED rather than throwing out of the reload.
    [Fact]
    public void GetTargetGoals_CyclicScopeMerges_RenderUnfolded_DoesNotThrow()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "Horsehead" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Horsehead", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="A", FilterName="Ha", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="Horsehead", Type=ImageType.Light, TelescopeName="B", FilterName="Ha", Exposure=3600, LibraryId=1 });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "A", CanonicalName = "B" });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "B", CanonicalName = "A" });
            db.SaveChanges();
        }

        var query = Query();
        var act = () => query.GetTargetGoals();
        act.Should().NotThrow();                                                          // cyclic set must not brick the tab
        act().Single(r => r.CanonicalName == "Horsehead").Scopes.Should().HaveCount(2);   // both scopes un-folded
    }

    // A goal saved while a scope's bare B rendered as imaging "Blue" must still attach after
    // photometric evidence (here a V frame) flips the cell's canonical label to "B" — the
    // stored label no longer matches, so the query's flip-partner fallback carries it over.
    // Without the fallback the goal silently vanishes from the UI.
    [Fact]
    public void GetTargetGoals_GoalSavedBeforePhotometricFlip_StillAttaches()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="b1", Path="/b1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="B", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="v1", Path="/v1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="V", Exposure=1800, LibraryId=1 });
            // Saved before the V run landed, when the scope's set was imaging-only and the
            // bare B canonicalized to "Blue".
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "Blue", GoalHours = 10 });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31").Scopes.Single();

        var b = scope.Filters.Single(f => f.Filter == "B");   // V present -> bare B is photometric
        b.ExplicitGoal.Should().Be(10);                        // the pre-flip "Blue" goal still attaches
        scope.Goal.Should().BeApproximately(10.5, 1e-6);       // and rolls up: 10 + V's unset 0.5h actual
    }

    // ADV-1 scenario B: the flip induced by a scope MERGE. The goal's scope key remaps to the
    // survivor AND the merged filter set flips the bare letter — the two mechanisms compose.
    [Fact]
    public void GetTargetGoals_ScopeMergeInducedFlip_GoalStillAttaches()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="m1", Path="/m1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30",  FilterName="B", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="m2", Path="/m2", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30b", FilterName="V", Exposure=1800, LibraryId=1 });
            // Goal saved pre-merge, when T30's set was imaging-only and its bare B rendered "Blue".
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "Blue", GoalHours = 10 });
            // Merging the photometric rig's scope in flips T30's bare B to "B".
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "T30b", CanonicalName = "T30" });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31").Scopes.Single();

        scope.Scope.Should().Be("T30");                        // merged into the survivor
        scope.Filters.Single(f => f.Filter == "B")             // V present post-merge -> photometric
            .ExplicitGoal.Should().Be(10);                     // the pre-merge "Blue" goal still attaches
    }

    // The live query sums Light frames only — a calibration frame's exposure must not inflate
    // the target's hours.
    [Fact]
    public void GetTargetGoals_ExcludesNonLightFrames()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="l1", Path="/l1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="d1", Path="/d1", ObjectName="M 31", Type=ImageType.Dark,  TelescopeName="T20", FilterName="L", Exposure=9999, LibraryId=1 });
            db.SaveChanges();
        }

        var m31 = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31");
        m31.Hours.Should().BeApproximately(1.0, 1e-6);   // the dark's 9999s is excluded
    }

    // The flip fallback must NOT borrow a goal that belongs to a coexisting partner cell: a
    // scope carrying a photometric bare "B" AND the imaging word "Blue" renders both cells,
    // and a goal stored under "Blue" belongs to the "Blue" cell only. Borrowing it for "B"
    // would display and roll up the same goal twice.
    [Fact]
    public void GetTargetGoals_FlipPartnerGoal_NotBorrowedWhenPartnerCellExists()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="p1", Path="/p1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="B",    Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="p2", Path="/p2", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="Blue", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="p3", Path="/p3", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="V",    Exposure=1800, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "Blue", GoalHours = 10 });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31").Scopes.Single();

        scope.Filters.Single(f => f.Filter == "Blue").ExplicitGoal.Should().Be(10);   // stays on its own cell
        scope.Filters.Single(f => f.Filter == "B").ExplicitGoal.Should().BeNull();    // not borrowed
    }

    // The reverse flip: a goal saved while the scope was photometric (stored as "B") must still
    // attach after the photometric evidence goes away (frames culled / merge undone) and the
    // bare letter renders as imaging "Blue" again.
    [Fact]
    public void GetTargetGoals_GoalSavedAsPhotometricLetter_AttachesAfterFlipBack()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="i1", Path="/i1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="B", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="i2", Path="/i2", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30", FilterName="L", Exposure=1800, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "B", GoalHours = 8 });
            db.SaveChanges();
        }

        var scope = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31").Scopes.Single();

        var blue = scope.Filters.Single(f => f.Filter == "Blue");   // no U/V/I, no word form -> imaging
        blue.ExplicitGoal.Should().Be(8);                            // the photometric-era "B" goal still attaches
    }

    // ---- Bucket-path coverage:
    // the three bucket paths below are where hours can silently under-count, so each boundary
    // is pinned explicitly.

    // A Light frame with a null ObjectName lands in the synthetic "(Unnamed)" pile.
    [Fact]
    public void GetTargetGoals_BucketsNullObjectName_AsUnnamed()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.Add(
                new ImageFile { HeaderHash="x", Path="/x", ObjectName=null, Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var rows = Query().GetTargetGoals();
        rows.Should().ContainSingle(r => r.CanonicalName == "(Unnamed)" && Math.Abs(r.Hours - 1.0) < 1e-6);
        rows.Single().TargetId.Should().Be(0);   // synthetic id — merge/goal-edit exclude it
    }

    // A non-empty ObjectName with no TargetNameMap must NOT be silently dropped by the inner
    // join: it surfaces under its own name as a synthetic row so its hours still count.
    [Fact]
    public void GetTargetGoals_BucketsNamedButUnmapped_UnderOwnName()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            // No TargetNameMap for "NGC 7000": an inner-join-only query would drop this frame.
            db.ImageFiles.Add(
                new ImageFile { HeaderHash="u1", Path="/u1", ObjectName="NGC 7000", Type=ImageType.Light, TelescopeName="T20", FilterName="Ha", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var rows = Query().GetTargetGoals();
        rows.Should().ContainSingle(r => r.CanonicalName == "NGC 7000" && Math.Abs(r.Hours - 1.0) < 1e-6);
    }

    // Whitespace-only ObjectNames belong in "(Unnamed)", not the named-but-unmapped bucket.
    // The tab variant proves the classification runs client-side — SQLite's TRIM strips only
    // spaces, so the SQL predicate alone can't catch it.
    [Fact]
    public void GetTargetGoals_BucketsWhitespaceObjectName_AsUnnamed()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="w1", Path="/w1", ObjectName="   ", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="w2", Path="/w2", ObjectName="\t",  Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=1800, LibraryId=1 });
            db.SaveChanges();
        }

        var rows = Query().GetTargetGoals();
        rows.Should().ContainSingle();
        rows.Single().CanonicalName.Should().Be("(Unnamed)");
        rows.Single().Hours.Should().BeApproximately(1.5, 1e-6);   // both whitespace frames fold together
    }

    // FITS space-padding ("M 31" vs "M 31 ") must not split a target's hours across two rows:
    // both frames trim to the same name, join the single map, and sum into ONE row.
    [Fact]
    public void GetTargetGoals_WhitespacePaddedObjectNames_FoldIntoOneRow()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="f1", Path="/f1", ObjectName="M 31",  Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 },
                new ImageFile { HeaderHash="f2", Path="/f2", ObjectName="M 31 ", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=1800, LibraryId=1 });
            db.SaveChanges();
        }

        var rows = Query().GetTargetGoals();
        rows.Should().ContainSingle();                             // no split row for the padded twin
        rows.Single().CanonicalName.Should().Be("M 31");
        rows.Single().Hours.Should().BeApproximately(1.5, 1e-6);   // both frames sum into the one mapped target
    }

    // A Light frame with null TelescopeName and null FilterName must bucket under the
    // "(Unknown scope)" / "(No filter)" defaults, not vanish or null-key a group.
    [Fact]
    public void GetTargetGoals_NullScopeAndFilter_UseDefaultBuckets()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.Add(
                new ImageFile { HeaderHash="n1", Path="/n1", ObjectName="M 31", Type=ImageType.Light, TelescopeName=null, FilterName=null, Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var m31 = Query().GetTargetGoals().Single(r => r.CanonicalName == "M 31");
        var scope = m31.Scopes.Should().ContainSingle().Which;
        scope.Scope.Should().Be("(Unknown scope)");
        scope.Filters.Should().ContainSingle(f => f.Filter == "(No filter)");
    }
}
