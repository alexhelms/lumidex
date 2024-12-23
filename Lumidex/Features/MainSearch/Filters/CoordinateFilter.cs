using Lumidex.Core.Data;

namespace Lumidex.Features.MainSearch.Filters;

public partial class CoordinateFilter : FilterViewModelBase
{
    private const decimal DefaultRadiusDegrees = 1.0m;

    [ObservableProperty]
    public partial decimal? RightAscension { get; set; }

    [ObservableProperty]
    public partial decimal? Declination { get; set; }

    [ObservableProperty]
    public partial decimal? Radius { get; set; } = DefaultRadiusDegrees;

    public override string DisplayName => "Coordinate";

    protected override void OnClear()
    {
        RightAscension = null;
        Declination = null;
        Radius = DefaultRadiusDegrees;
    }

    [RelayCommand]
    private void ClearRightAscension() => RightAscension = null;
    
    [RelayCommand]
    private void ClearDeclination() => Declination = null;

    [RelayCommand]
    private void ClearRadius() => Radius = null;

    public override IQueryable<ImageFile> ApplyFilter(LumidexDbContext dbContext, IQueryable<ImageFile> query)
    {
        if (RightAscension.HasValue && Declination.HasValue && Radius.HasValue)
        {
            // Convert hours to degrees
            double ra = (double)RightAscension.Value * 15.0;
            double dec = (double)Declination.Value;
            double radius = (double)Radius.Value;
            
            // EF does not support mixing IQueryable and raw sql.
            // A workaround is to find the matching image IDs and
            // use a hashset to filter the original IQueryable.
            var matchingImageIdSet = RaDecSearch.Search(dbContext, ra, dec, radius)
                .Select(image => image.Id)
                .ToHashSet();

            return query = query.Where(f => matchingImageIdSet.Contains(f.Id));
        }

        return query;
    }

    public override PersistedFilter? Persist()
    {
        if (RightAscension is null && Declination is null && Radius == DefaultRadiusDegrees)
            return null;

        var ra = RightAscension.HasValue ? RightAscension.Value.ToString() : string.Empty;
        var dec = Declination.HasValue ? Declination.Value.ToString() : string.Empty;
        var radius = Radius.HasValue ? Radius.Value.ToString() : string.Empty;

        return new PersistedFilter
        {
            Name = "Coordinate",
            Data = $"{ra}|{dec}|{radius}",
        };
    }

    public override bool Restore(PersistedFilter persistedFilter)
    {
        var restored = false;

        if (persistedFilter.Name == "Coordinate")
        {
            var data = persistedFilter.Data ?? string.Empty;
            var split = data.Split('|', count: 3);
            if (split.Length == 3)
            {
                if (decimal.TryParse(split[0], out var ra))
                {
                    RightAscension = ra;
                    restored = true;
                }

                if (decimal.TryParse(split[1], out var dec))
                {
                    Declination = dec;
                    restored = true;
                }

                if (decimal.TryParse(split[2], out var radius))
                {
                    Radius = radius;
                    restored = true;
                }
            }
        }

        return restored;
    }

    public override string ToString() => $"{DisplayName} = ({RightAscension}, {Declination}, {Radius})";
}
