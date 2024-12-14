using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ImageKindFilter : FilterViewModelBase
{
    [ObservableProperty]
    public partial ImageKind? ImageKind { get; set; }

    public override string DisplayName => "Image Kind";

    public List<ImageKind> ImageKinds { get; } = Enum.GetValues<ImageKind>().OrderBy(x => x.ToString()).ToList();

    protected override void OnClear() => ImageKind = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (ImageKind is { } imageKind)
        {
            query = query.Where(f => f.Kind == imageKind);
        }

        return query;
    }

    public override PersistedFilter? Persist() => ImageKind is null
        ? null
        : new PersistedFilter
        {
            Name = "ImageKind",
            Data = ImageKind.ToString(),
        };

    public override bool Restore(PersistedFilter persistedFilter)
    {
        if (persistedFilter.Name == "ImageKind" &&
            Enum.TryParse<ImageKind>(persistedFilter.Data, out var imageKind))
        {
            ImageKind = imageKind;
            return true;
        }

        return false;
    }

    public override string ToString() => $"{DisplayName} = {ImageKind}";
}
