using Lumidex.Core.Data;
using Lumidex.Core.Targets;
using Lumidex.Features.TargetSummary;
using Lumidex.Services;
using Lumidex.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests;

// The schema is wiped/recreated per test so seeded rows never leak between tests.
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
        return new TargetSummaryViewModel(factory, new TargetResolutionService(factory), new TargetGoalQuery(factory), new DialogService());
    }

    // A goal entered on a filter row persists to TargetFilterGoal for that (target, scope, filter).
    [Fact]
    public async Task SetFilterGoal_StoresGoalOnTargetScopeFilter()
    {
        int targetId;
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.Add(new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
            targetId = t.Id;
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var filter = vm.Targets.Single().Scopes.Single().Filters.Single();
        filter.ExplicitGoal = 10;
        await filter.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetFilterGoals.Single(g => g.TargetId == targetId && g.Scope == "T20" && g.Filter == "L")
            .GoalHours.Should().Be(10);
    }

    // A zero (or cleared) goal deletes the filter's goal row — PercentComplete treats "no goal"
    // as goal == acquired, so storing 0 would just be an ignored value.
    [Fact]
    public async Task SetFilterGoal_Zero_DeletesGoal()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.Add(new ImageFile { HeaderHash="a", Path="/a", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T20", Filter = "L", GoalHours = 10 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var filter = vm.Targets.Single().Scopes.Single().Filters.Single();
        filter.ExplicitGoal = 0;
        await filter.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetFilterGoals.Should().BeEmpty();
    }

    // Merging the selected target rows collapses them to one target.
    [Fact]
    public async Task MergeSelected_MergesSelectedTargets()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Andromeda", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=7200, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31",      Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        vm.Targets.Should().HaveCount(2);                 // distinct names stay separate
        foreach (var t in vm.Targets) t.IsSelected = true;
        await vm.MergeSelectedCommand.ExecuteAsync(null);

        vm.Targets.Should().ContainSingle();              // the two rows fold to one in the view
        vm.Targets.Single().CanonicalName.Should().Be("Andromeda"); // survivor = most-integrated (2h > 1h)

        using var verify = new LumidexDbContext(_fx.Options);
        verify.Targets.Should().HaveCount(2);             // non-destructive: both target rows remain on disk
        verify.TargetMerges.Should().ContainSingle();     // the fold is a record, undoable
    }

    // F16: undoing the last merge from the flyout empties the list and flips the empty-state flag, so
    // "No merges yet." shows and no orphaned row lingers. Exercises the MergeOperationRowViewModel
    // UndoCommand callback path end to end (the flyout's actual binding).
    [Fact]
    public async Task UndoMerge_LastOperation_EmptiesPanelAndFlipsEmptyState()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            db.ImageFiles.AddRange(
                new ImageFile { HeaderHash="a", Path="/a", ObjectName="Andromeda", Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=7200, LibraryId=1 },
                new ImageFile { HeaderHash="b", Path="/b", ObjectName="M 31",      Type=ImageType.Light, TelescopeName="T20", FilterName="L", Exposure=3600, LibraryId=1 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        foreach (var t in vm.Targets) t.IsSelected = true;
        await vm.MergeSelectedCommand.ExecuteAsync(null);
        vm.MergeOperations.Should().ContainSingle();          // the merge shows in the panel
        vm.HasNoMerges.Should().BeFalse();

        await vm.MergeOperations.Single().UndoCommand.ExecuteAsync(null);

        vm.MergeOperations.Should().BeEmpty();                // undo clears it
        vm.HasNoMerges.Should().BeTrue();                     // empty-state flips
        vm.Targets.Should().HaveCount(2);                     // and the rows re-separate
    }

    // Editing a FLIPPED cell (goal stored pre-flip as "Blue", cell renders "B") must update
    // the stored row and re-key it to the rendered label — not strand it and insert a twin
    // row for the same channel.
    [Fact]
    public async Task SetFilterGoal_OnFlippedCell_UpdatesPartnerRow_NoTwin()
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
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "Blue", GoalHours = 10 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var b = vm.Targets.Single().Scopes.Single().Filters.Single(f => f.Filter == "B");
        b.ExplicitGoal.Should().Be(10);                       // the flipped cell displays the stored goal
        b.ExplicitGoal = 12;
        await b.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        var row = verify.TargetFilterGoals.Should().ContainSingle().Which;   // updated, not twinned
        row.Filter.Should().Be("B");                                          // re-keyed to the rendered label
        row.GoalHours.Should().Be(12);
    }

    // Clearing a flipped cell must delete the partner-labelled row it displayed — leaving it
    // stranded would resurrect the goal on the next reload.
    [Fact]
    public async Task SetFilterGoal_Zero_OnFlippedCell_DeletesPartnerRow()
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
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30", Filter = "Blue", GoalHours = 10 });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var b = vm.Targets.Single().Scopes.Single().Filters.Single(f => f.Filter == "B");
        b.ExplicitGoal = 0;
        await b.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetFilterGoals.Should().BeEmpty();          // the pre-flip row is gone, no resurrection
    }

    // A goal stored under an ABSORBED scope name renders on the survivor's cell (the read side
    // resolves merges); editing that cell must update the stored row in place — re-keyed to the
    // survivor — not fork a second row under the survivor label.
    [Fact]
    public async Task SetFilterGoal_OnGoalUnderAbsorbedScope_UpdatesInPlace_NoTwin()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.Add(
                new ImageFile { HeaderHash="a1", Path="/a1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30b", FilterName="L", Exposure=3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30b", Filter = "L", GoalHours = 10 });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "T30b", CanonicalName = "T30" });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var l = vm.Targets.Single().Scopes.Single().Filters.Single(f => f.Filter == "L");
        l.ExplicitGoal.Should().Be(10);                       // displayed via the merge remap
        l.ExplicitGoal = 12;
        await l.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        var row = verify.TargetFilterGoals.Should().ContainSingle().Which;   // updated, not forked
        row.Scope.Should().Be("T30");                                         // re-keyed to the survivor
        row.GoalHours.Should().Be(12);
    }

    // Clearing a goal displayed via the merge remap must delete the pre-merge row — the
    // pre-fix literal-key search found nothing, silently no-op'd, and the goal resurrected.
    [Fact]
    public async Task SetFilterGoal_Zero_OnGoalUnderAbsorbedScope_DeletesIt()
    {
        using (var db = new LumidexDbContext(_fx.Options))
        {
            db.Libraries.Add(new Library { Id = 1, Name = "Lib", Path = "/lib" });
            var t = new Target { CanonicalName = "M 31" };
            db.Targets.Add(t);
            db.TargetNameMaps.Add(new TargetNameMap { RawObjectName = "M 31", Target = t });
            db.ImageFiles.Add(
                new ImageFile { HeaderHash="a1", Path="/a1", ObjectName="M 31", Type=ImageType.Light, TelescopeName="T30b", FilterName="L", Exposure=3600, LibraryId=1 });
            db.TargetFilterGoals.Add(new TargetFilterGoal { Target = t, Scope = "T30b", Filter = "L", GoalHours = 10 });
            db.ScopeMerges.Add(new ScopeMerge { OperationId = Guid.NewGuid(), AbsorbedName = "T30b", CanonicalName = "T30" });
            db.SaveChanges();
        }

        var vm = BuildViewModel();
        await vm.Reload();
        var l = vm.Targets.Single().Scopes.Single().Filters.Single(f => f.Filter == "L");
        l.ExplicitGoal = 0;
        await l.SetGoalCommand.ExecuteAsync(null);

        using var verify = new LumidexDbContext(_fx.Options);
        verify.TargetFilterGoals.Should().BeEmpty();          // deleted through the merge remap
    }
}
