namespace Lumidex.Core.Data;

public class ImageFileFilters
{
    public string? Name { get; init; }
    public int? LibraryId { get; init; }
    public ImageType? ImageType { get; init; }
    public ImageKind? ImageKind { get; init; }
    public TimeSpan? ExposureMin { get; init; }
    public TimeSpan? ExposureMax { get; init; }
    public string? Filter { get; init; }
    public DateTime? DateBegin { get; init; }
    public DateTime? DateEnd { get; init; }
    public IEnumerable<int>? TagIds { get; init; }
}
