using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class ImageKindFilter : FilterViewModelBase
{
    [ObservableProperty] ImageKind? _imageKind;

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

    public override string ToString() => $"{DisplayName} = {ImageKind}";
}
