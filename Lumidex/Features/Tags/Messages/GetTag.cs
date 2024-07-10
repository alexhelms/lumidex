using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex.Features.Tags.Messages;

public class GetTag : RequestMessage<TagViewModel>
{
    public required int Id { get; init; }
}
