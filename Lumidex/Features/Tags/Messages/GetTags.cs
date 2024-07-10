using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Lumidex.Features.Tags.Messages;

public class GetTags : RequestMessage<IList<TagViewModel>>
{
}
