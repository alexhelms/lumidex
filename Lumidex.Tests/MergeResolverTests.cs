using Lumidex.Core.Data;
using Lumidex.Core.Targets;

namespace Lumidex.Tests;

// Pure (no DB) tests of the absorbed -> survivor resolution the query applies in memory: basic
// folds, transitive chains, and the bounded-loop cycle guard.
public class MergeResolverTests
{
    private static ScopeMerge Scope(string absorbed, string canonical)
        => new() { AbsorbedName = absorbed, CanonicalName = canonical, OperationId = Guid.NewGuid() };

    private static TargetMerge Target(int absorbed, int survivor)
        => new() { AbsorbedTargetId = absorbed, SurvivorTargetId = survivor, OperationId = Guid.NewGuid(),
                   SurvivorLabel = "s", AbsorbedLabel = "a" };

    [Fact]
    public void BuildScopeMap_FoldsAbsorbedToCanonical_CaseInsensitiveKey()
    {
        var map = MergeResolver.BuildScopeMap(new[] { Scope("iTelescope 75", "iTelescope T75") });

        map["itelescope 75"].Should().Be("iTelescope T75");   // key lowercased, value keeps case
    }

    [Fact]
    public void BuildScopeMap_ResolvesTransitiveChain_ToTerminal()
    {
        // A -> B and B -> C: a frame logged as A must resolve all the way to C.
        var map = MergeResolver.BuildScopeMap(new[] { Scope("A", "B"), Scope("B", "C") });

        map["a"].Should().Be("C");
        map["b"].Should().Be("C");
    }

    // A cyclic record set has no terminal survivor; the cyclic keys are OMITTED from the map (their
    // rows show un-folded) instead of throwing, so a reload never bricks.
    [Fact]
    public void BuildScopeMap_Cycle_OmitsCyclicKeys_DoesNotThrow()
    {
        var merges = new[] { Scope("A", "B"), Scope("B", "A") };

        var build = () => MergeResolver.BuildScopeMap(merges);
        build.Should().NotThrow();
        build().Should().BeEmpty();                          // both keys cyclic -> nothing folds
    }

    [Fact]
    public void BuildScopeMap_Empty_IsEmpty()
    {
        MergeResolver.BuildScopeMap(Array.Empty<ScopeMerge>()).Should().BeEmpty();
    }

    [Fact]
    public void BuildTargetMap_FoldsAbsorbedToSurvivor()
    {
        var map = MergeResolver.BuildTargetMap(new[] { Target(2, 1) });

        map[2].Should().Be(1);
    }

    [Fact]
    public void BuildTargetMap_ResolvesTransitiveChain_ToTerminal()
    {
        // 3 -> 2 and 2 -> 1: both resolve to the terminal survivor 1.
        var map = MergeResolver.BuildTargetMap(new[] { Target(3, 2), Target(2, 1) });

        map[3].Should().Be(1);
        map[2].Should().Be(1);
    }

    [Fact]
    public void BuildTargetMap_Cycle_OmitsCyclicKeys_DoesNotThrow()
    {
        var merges = new[] { Target(1, 2), Target(2, 1) };

        var build = () => MergeResolver.BuildTargetMap(merges);
        build.Should().NotThrow();
        build().Should().BeEmpty();
    }
}
