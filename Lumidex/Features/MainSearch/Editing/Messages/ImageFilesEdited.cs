﻿namespace Lumidex.Features.MainSearch.Editing.Messages;

public class ImageFilesEdited
{
    public required IReadOnlyList<ImageFileViewModel> ImageFiles { get; init; } = [];
}
