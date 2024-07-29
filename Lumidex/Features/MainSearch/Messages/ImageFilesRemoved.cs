namespace Lumidex.Features.MainSearch.Messages;

public class ImageFilesRemoved
{
    public required IReadOnlyList<ImageFileViewModel> ImageFiles { get; init; } = [];
}