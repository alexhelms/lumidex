using Lumidex.Core.Data;

namespace Lumidex.Core.Targets;

// Which kind of thing a user merge folded — drives the icon/label in the Manage-merges panel.
public enum MergeKind { Scope, Target }

// One user merge action, flattened for the Manage-merges panel: the survivor it kept and the names
// it absorbed. OperationId is the handle Undo passes back to TargetResolutionService.UndoMerge.
public record MergeOperation(
    Guid OperationId,
    MergeKind Kind,
    string SurvivorLabel,
    IReadOnlyList<string> AbsorbedLabels,
    DateTime CreatedUtc);

// Turns the stored merge records into the absorbed -> survivor lookups the query applies in memory
// before the roll-up. The maps are TRANSITIVE (A->B plus B->C resolves A->C, which happens when a
// survivor is later re-merged into a newer canonical). A record set that loops back on itself (a
// cycle) has no terminal survivor; such keys are OMITTED from the map — their rows simply show
// un-folded — rather than throwing, so a corrupt or cyclic set can never brick the reload. A cycle
// is unreachable through the UI (an absorbed row folds away and can't be re-selected); this totality
// is the safety net for any that a future caller or an odd consolidation sequence might produce.
// Pure (no DB) so the resolution logic is unit-testable on its own.
public static class MergeResolver
{
    // A real fold depth is 1-2; a chain longer than this is a cycle or corruption, not a deeper
    // merge. The bound makes the resolve loop terminate on a cyclic set instead of looping forever.
    private const int MaxChain = 32;

    // absorbed TelescopeName (lowercased) -> terminal canonical name. Scope names compare
    // case-insensitively (the library logs "T20"/"t20"); the query lowercases its scope keys too.
    public static IReadOnlyDictionary<string, string> BuildScopeMap(IEnumerable<ScopeMerge> merges)
    {
        var direct = new Dictionary<string, string>();
        foreach (var m in merges)
            direct[m.AbsorbedName.ToLowerInvariant()] = m.CanonicalName;

        var resolved = new Dictionary<string, string>();
        foreach (var absorbed in direct.Keys)
            if (TryResolveString(direct, absorbed, out var terminal))
                resolved[absorbed] = terminal;   // a cyclic key is omitted -> its row shows un-folded
        return resolved;
    }

    // absorbed TargetId -> terminal survivor id.
    public static IReadOnlyDictionary<int, int> BuildTargetMap(IEnumerable<TargetMerge> merges)
    {
        var direct = new Dictionary<int, int>();
        foreach (var m in merges)
            direct[m.AbsorbedTargetId] = m.SurvivorTargetId;

        var resolved = new Dictionary<int, int>();
        foreach (var absorbed in direct.Keys)
            if (TryResolveInt(direct, absorbed, out var terminal))
                resolved[absorbed] = terminal;
        return resolved;
    }

    // Follow direct[start] until the value is not itself an absorbed key (the terminal survivor).
    // Returns false if the chain exceeds MaxChain (a cycle), so the caller omits the key.
    private static bool TryResolveString(IReadOnlyDictionary<string, string> direct, string startKey, out string terminal)
    {
        var current = direct[startKey];
        for (var hops = 0; hops < MaxChain; hops++)
        {
            if (!direct.TryGetValue(current.ToLowerInvariant(), out var next))
            {
                terminal = current;
                return true;
            }
            current = next;
        }
        terminal = current;
        return false;
    }

    private static bool TryResolveInt(IReadOnlyDictionary<int, int> direct, int startKey, out int terminal)
    {
        var current = direct[startKey];
        for (var hops = 0; hops < MaxChain; hops++)
        {
            if (!direct.TryGetValue(current, out var next))
            {
                terminal = current;
                return true;
            }
            current = next;
        }
        terminal = current;
        return false;
    }
}
