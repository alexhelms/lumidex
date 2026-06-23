using Lumidex.Core.Targets;

namespace Lumidex.Features.TargetSummary;

// One row in the "Manage merges" flyout: a user merge (scope or target) with a human label and the
// OperationId needed to reverse it. The Undo button binds to this row's own UndoCommand (a callback
// the VM assigns), not an ancestor binding — same pattern as the filter row's goal persistence.
public partial class MergeOperationRowViewModel : ObservableObject
{
    public required Guid OperationId { get; init; }
    public required MergeKind Kind { get; init; }

    // e.g. "iTelescope 75 → iTelescope T75" (scope) or "Barnard 33 → Horsehead" (target).
    public required string Label { get; init; }

    // A short tag shown beside the label so scope vs target merges are distinguishable at a glance.
    public string KindLabel => Kind == MergeKind.Scope ? "Scopes" : "Targets";

    // Assigned by the VM; invoked by the Undo button to remove the records and reload.
    public Func<MergeOperationRowViewModel, Task>? UndoAction { get; set; }

    [RelayCommand]
    private Task Undo() => UndoAction?.Invoke(this) ?? Task.CompletedTask;
}
