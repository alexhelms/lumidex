using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Features.MainSearch.Messages;

public class Search
{
    public IReadOnlyList<FilterViewModelBase> Filters { get; init; } = [];
}
