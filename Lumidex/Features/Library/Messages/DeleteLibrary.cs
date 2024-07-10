namespace Lumidex.Features.Library.Messages;

public class DeleteLibrary
{
    public required Common.LibraryViewModel Library { get; init; }
}