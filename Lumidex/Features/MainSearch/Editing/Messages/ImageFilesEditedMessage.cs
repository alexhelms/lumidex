namespace Lumidex.Features.MainSearch.Editing.Messages;

public class ImageFilesEditedMessage
{
    public required IList<ImageFileViewModel> ImageFiles { get; init; } = [];
}
