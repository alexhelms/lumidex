using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests;

// The user-facing merge surface, now NON-DESTRUCTIVE: MergeScopes / MergeTargets record an
// absorbed -> survivor mapping instead of deleting/repointing, and UndoMerge removes the record.
// The visible folding is verified in TargetGoalQueryTests; here we pin the records + the
// "nothing is destroyed" contract that makes Undo possible.
public class MergeServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DatabaseFixture _fx;
    public MergeServiceTests(DatabaseFixture fx)
    {
        _fx = fx;
        using var db = new LumidexDbContext(_fx.Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
    public void Dispose() => GC.SuppressFinalize(this);

    private TargetResolutionService Service() => new(new TestDbContextFactory(_fx.DatabaseFilename));

    // The whole point of going non-destructive: a target merge must NOT delete the absorbed target,
    // repoint its maps, touch its goals, or even rename the survivor — so Undo is a perfect inverse.
    // It only records a TargetMerge. Passing a canonical name DIFFERENT from the survivor's proves the
    // survivor is left untouched; this FAILS on the old code (which renamed and deleted).
    [Fact]
    public void MergeTargets_RecordsMapping_LeavesAbsorbedGoalsAndSurvivorNameIntact()
    {
        int survivorId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "M 31" };
            var absorbed = new Target { CanonicalName = "Andromeda" };
            db.Targets.AddRange(survivor, absorbed);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = survivor });
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "Andromeda", Target = absorbed });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = survivor, Scope = "T20", Filter = "L", GoalHours = 10 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = absorbed, Scope = "T30", Filter = "L", GoalHours = 20 });
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id;
        }

        Service().MergeTargets(new[] { survivorId, absorbedId }, "Andromeda Galaxy");

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().HaveCount(2);                                    // absorbed NOT deleted
        verify.Targets.Single(t => t.Id == survivorId).CanonicalName.Should().Be("M 31");      // survivor name UNTOUCHED
        verify.Targets.Single(t => t.Id == absorbedId).CanonicalName.Should().Be("Andromeda"); // absorbed name kept
        verify.TargetNameMaps.Single(m => m.RawObjectName == "Andromeda").TargetId.Should().Be(absorbedId); // map NOT repointed
        verify.TargetFilterGoals.Should().HaveCount(2);                          // both goals intact, untouched
        var record = verify.TargetMerges.Should().ContainSingle().Subject;
        record.SurvivorTargetId.Should().Be(survivorId);
        record.AbsorbedTargetId.Should().Be(absorbedId);
        record.AbsorbedLabel.Should().Be("Andromeda");
    }

    // F14: the canonical name is stored trimmed, like the absorbed names — no padded-canonical that
    // would land on a different roll-up bucket than a genuine same-name scope.
    [Fact]
    public void MergeScopes_TrimsCanonicalName()
    {
        Service().MergeScopes("iTelescope T75 ", new[] { "iTelescope 75" });

        using var verify = new LumidexDbContext(_fx.Options);
        verify.ScopeMerges.Single().CanonicalName.Should().Be("iTelescope T75");   // trailing space trimmed
    }

    // Auto-consolidation can rename a merge survivor after the merge; the panel must show the
    // survivor's LIVE name, not the stale snapshot, so it matches the folded row.
    [Fact]
    public void GetMergeOperations_TargetSurvivorRenamed_ShowsLiveName()
    {
        int survivorId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "Barnard 33" };
            var absorbed = new Target { CanonicalName = "Horsehead" };
            db.Targets.AddRange(survivor, absorbed);
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id;
        }

        var svc = Service();
        svc.MergeTargets(new[] { survivorId, absorbedId }, "Barnard 33");

        // simulate auto-consolidation renaming the survivor to the new dominant spelling
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Targets.Single(t => t.Id == survivorId).CanonicalName = "B33";
            db.SaveChanges();
        }

        var op = svc.GetMergeOperations().Single(o => o.Kind == MergeKind.Target);
        op.SurvivorLabel.Should().Be("B33");                                  // live name, not the "Barnard 33" snapshot
    }

    // < 2 distinct ids is a no-op (a row "merged with itself" via a repeated id writes no record).
    [Fact]
    public void MergeTargets_FewerThanTwoDistinct_WritesNoRecord()
    {
        int onlyId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.SaveChanges();
            onlyId = t.Id;
        }

        Service().MergeTargets(new[] { onlyId, onlyId }, "M 31");

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetMerges.Should().BeEmpty();
    }

    // A repeated survivor id must not produce a survivor->survivor record; Distinct() collapses it
    // so only the real absorbed mapping is written.
    [Fact]
    public void MergeTargets_DuplicateSurvivorId_WritesOnlyTheAbsorbedRecord()
    {
        int survivorId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "M 31" };
            var absorbed = new Target { CanonicalName = "Andromeda" };
            db.Targets.AddRange(survivor, absorbed);
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id;
        }

        Service().MergeTargets(new[] { survivorId, survivorId, absorbedId }, "Andromeda Galaxy");

        using var verify = new LumidexDbContext(_fx.Options);
        var record = verify.TargetMerges.Should().ContainSingle().Subject;       // no survivor->survivor row
        record.SurvivorTargetId.Should().Be(survivorId);
        record.AbsorbedTargetId.Should().Be(absorbedId);
    }

    // An id already absorbed by an earlier merge is skipped so the read-time remap stays
    // single-valued (one survivor per absorbed id).
    [Fact]
    public void MergeTargets_AlreadyAbsorbedId_IsSkipped()
    {
        int survivorId, absorbedId, otherId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "M 31" };
            var absorbed = new Target { CanonicalName = "Andromeda" };
            var other = new Target { CanonicalName = "M 32" };
            db.Targets.AddRange(survivor, absorbed, other);
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id; otherId = other.Id;
        }

        var svc = Service();
        svc.MergeTargets(new[] { survivorId, absorbedId }, "Andromeda Galaxy");   // absorbed -> survivor
        svc.MergeTargets(new[] { otherId, absorbedId }, "M 32");                  // try to re-absorb 'absorbed'

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetMerges.Where(m => m.AbsorbedTargetId == absorbedId).Should().ContainSingle(); // not re-recorded
        verify.TargetMerges.Single(m => m.AbsorbedTargetId == absorbedId).SurvivorTargetId.Should().Be(survivorId);
    }

    // A vanished survivor (deleted between render and merge) is a no-op: no record, absorbed
    // untouched — never escapes to the global handler.
    [Fact]
    public void MergeTargets_VanishedSurvivor_IsNoOp()
    {
        int absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var absorbed = new Target { CanonicalName = "Andromeda" };
            db.Targets.Add(absorbed);
            db.SaveChanges();
            absorbedId = absorbed.Id;
        }

        var merge = () => Service().MergeTargets(new[] { absorbedId + 1000, absorbedId }, "Andromeda Galaxy");

        merge.Should().NotThrow();
        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetMerges.Should().BeEmpty();
        verify.Targets.Single().CanonicalName.Should().Be("Andromeda");
    }

    // Scope merge records each absorbed telescope-name -> canonical, sharing one OperationId.
    [Fact]
    public void MergeScopes_RecordsAbsorbedToCanonical()
    {
        Service().MergeScopes("iTelescope T75", new[] { "iTelescope 75 ", "iTelescope 76" });

        using var verify = new LumidexDbContext(_fx.Options);
        verify.ScopeMerges.Should().HaveCount(2);
        verify.ScopeMerges.Select(m => m.AbsorbedName).Should().BeEquivalentTo(new[] { "iTelescope 75", "iTelescope 76" }); // first trimmed
        verify.ScopeMerges.Select(m => m.CanonicalName).Should().AllBe("iTelescope T75");
        verify.ScopeMerges.Select(m => m.OperationId).Distinct().Should().ContainSingle(); // one operation
    }

    // The canonical itself, an in-call duplicate, and a name already folded by an earlier merge are
    // all skipped so the absorbed -> canonical remap never becomes many-valued.
    [Fact]
    public void MergeScopes_SkipsSelfDuplicateAndAlreadyAbsorbed()
    {
        var svc = Service();
        svc.MergeScopes("T75", new[] { "T75old" });                              // first fold
        svc.MergeScopes("T75", new[] { "T75", "T75 ", "T80", "T80", "T75old" }); // self, padded-self, dup, already-absorbed

        using var verify = new LumidexDbContext(_fx.Options);
        verify.ScopeMerges.Select(m => m.AbsorbedName).OrderBy(x => x)
            .Should().BeEquivalentTo(new[] { "T75old", "T80" });                 // only the one new, valid name added
    }

    // Undo removes exactly the records of one operation (scope or target), leaving others.
    [Fact]
    public void UndoMerge_RemovesOnlyThatOperationsRecords()
    {
        int survivorId, absorbedId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "M 31" };
            var absorbed = new Target { CanonicalName = "Andromeda" };
            db.Targets.AddRange(survivor, absorbed);
            db.SaveChanges();
            survivorId = survivor.Id; absorbedId = absorbed.Id;
        }

        var svc = Service();
        svc.MergeScopes("T75", new[] { "T75alt" });
        svc.MergeTargets(new[] { survivorId, absorbedId }, "Andromeda Galaxy");

        var ops = svc.GetMergeOperations();
        var scopeOp = ops.Single(o => o.Kind == MergeKind.Scope);
        svc.UndoMerge(scopeOp.OperationId);

        using var verify = new LumidexDbContext(_fx.Options);
        verify.ScopeMerges.Should().BeEmpty();                  // the undone scope merge is gone
        verify.TargetMerges.Should().ContainSingle();           // the target merge is untouched
    }

    // The panel feed: scope and target merges unify into one MergeOperation list, each grouped by
    // OperationId (a 3-way merge is one operation carrying two absorbed labels).
    [Fact]
    public void GetMergeOperations_UnifiesScopeAndTarget_GroupsByOperation()
    {
        int survivorId, aId, bId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            var survivor = new Target { CanonicalName = "M 31" };
            var a = new Target { CanonicalName = "Andromeda" };
            var b = new Target { CanonicalName = "And." };
            db.Targets.AddRange(survivor, a, b);
            db.SaveChanges();
            survivorId = survivor.Id; aId = a.Id; bId = b.Id;
        }

        var svc = Service();
        svc.MergeScopes("T75", new[] { "T75a", "T75b" });
        svc.MergeTargets(new[] { survivorId, aId, bId }, "M 31");   // UI passes the survivor's own name

        var ops = svc.GetMergeOperations();
        ops.Should().HaveCount(2);                                               // one scope op + one target op

        var scope = ops.Single(o => o.Kind == MergeKind.Scope);
        scope.SurvivorLabel.Should().Be("T75");
        scope.AbsorbedLabels.Should().BeEquivalentTo(new[] { "T75a", "T75b" });

        var target = ops.Single(o => o.Kind == MergeKind.Target);
        target.SurvivorLabel.Should().Be("M 31");                                 // survivor's live name, not the stale stored label
        target.AbsorbedLabels.Should().BeEquivalentTo(new[] { "Andromeda", "And." }); // both absorbed in one op
    }
}
