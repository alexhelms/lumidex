namespace Lumidex.Features.MainSearch.Messages;

public class RemoveImageFiles
{
    public required IReadOnlyList<ImageFileViewModel> ImageFiles { get; init; } = [];
}
