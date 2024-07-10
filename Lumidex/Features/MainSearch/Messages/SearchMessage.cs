using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Messages;

public class SearchMessage
{
    public ImageFileFilters Filters { get; init; } = new();
}
