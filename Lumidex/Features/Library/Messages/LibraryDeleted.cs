﻿namespace Lumidex.Features.Library.Messages;

public class LibraryDeleted
{
    public required Common.LibraryViewModel Library { get; init; }
}
