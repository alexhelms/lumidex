using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Messages;

public class QueryMessage
{
    public string? ObjectName { get; set; }
    public ImageType? ImageType { get; set; }
    public ImageKind? ImageKind { get; set; }
    public TimeSpan? ExposureMin { get; set; }
    public TimeSpan? ExposureMax { get; set; }
    public string? Filter { get; set; }
    public DateTime? DateBegin { get; set; }
    public DateTime? DateEnd { get; set; }
}
