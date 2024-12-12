using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ImageTypeFilter : FilterViewModelBase
{
    [ObservableProperty] ImageType? _imageType;

    public override string DisplayName => "Image Type";

    public List<ImageType> ImageTypes { get; } = Enum.GetValues<ImageType>().OrderBy(x => x.ToString()).ToList();

    protected override void OnClear() => ImageType = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (ImageType is { } imageType)
        {
            query = query.Where(f => f.Type == imageType);
        }

        return query;
    }

    public override PersistedFilter? Persist() => ImageType is null
        ? null
        : new PersistedFilter
        {
            Name = "ImageType",
            Data = ImageType.ToString(),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "ImageType" &&
            Enum.TryParse<ImageType>(persistedFilter.Data, out var imageType))
        {
            ImageType = imageType;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {ImageType}";
}
