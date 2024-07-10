namespace Lumidex.Features.Library.Messages;

public class LibraryCreated
{
    public required Common.LibraryViewModel Library { get; init; }
}
