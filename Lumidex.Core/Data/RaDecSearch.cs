using Microsoft.EntityFrameworkCore;

namespace Lumidex.Core.Data;

public static class RaDecSearch
{
    public static IQueryable<ImageFile> Search(this LumidexDbContext dbContext, double ra, double dec, double radius)
    {
        ra = Math.Clamp(ra, 0, 360);
        dec = Math.Clamp(dec, -90, 90);
        radius = Math.Clamp(radius, 0, double.MaxValue);

        FormattableString sql = $"""
            WITH const as (SELECT {radius} as radius, {ra} as RA, {dec} as DEC)
            SELECT *
            FROM ImageFiles, const
            WHERE Id IN (
            	SELECT Id
            	FROM RaDecRTree
            	WHERE 1 = 1
            		AND MinRa >= (const.RA - const.radius)
            		AND MaxRa <= (const.RA + const.radius)
            		AND MinDec >= (const.DEC - const.radius)
            		AND MaxDec <= (const.DEC + const.radius)
            )
            """;

        return dbContext.ImageFiles
            .FromSql(sql)
            .AsNoTracking();
    }
}
