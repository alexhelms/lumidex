namespace Lumidex.Features.MainSearch.Editing.Messages;

public class ImageFilesEdited
{
    public required IList<ImageFileViewModel> ImageFiles { get; init; } = [];
}
