using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Features.MainSearch.Messages;

public class SearchStarting
{
    public required IReadOnlyList<FilterViewModelBase> Filters { get; init; } = [];
}
