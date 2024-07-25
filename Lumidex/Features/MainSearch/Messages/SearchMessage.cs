using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Features.MainSearch.Messages;

public class SearchMessage
{
    public IReadOnlyList<FilterViewModelBase> Filters { get; init; } = [];
}
