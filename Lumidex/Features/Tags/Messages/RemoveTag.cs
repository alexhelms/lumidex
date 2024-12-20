﻿namespace Lumidex.Features.Tags.Messages;

public class RemoveTag
{
    public required TagViewModel Tag { get; init; }
    public required IEnumerable<ImageFileViewModel> ImageFiles { get; init; } = [];
}
