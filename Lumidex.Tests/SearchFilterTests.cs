using Lumidex.Core.Data;
using Lumidex.Tests.Fixtures;
using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Tests;

public class SearchFilterTests
{
    private static DatabaseFixture CreateDatabaseFixture()
    {
        var fixture = new DatabaseFixture();
        SeedDatabase(fixture.DbContext);
        return fixture;
    }

    private static void SeedDatabase(LumidexDbContext dbContext)
    {
        var library = new Core.Data.Library
        {
            Name = "default",
            Path = @"/tmp",
        };

        dbContext.Libraries.Add(library);

        dbContext.ImageFiles.AddRange([
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "L",
                Path = @"/tmp/image-L.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "R",
                Path = @"/tmp/image-R.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "G",
                Path = @"/tmp/image-G.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "B",
                Path = @"/tmp/image-B.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "Ha",
                Path = @"/tmp/image-Ha.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "Sii",
                Path = @"/tmp/image-Sii.fits",
            },
            new Core.Data.ImageFile
            {
                HeaderHash = Guid.NewGuid().ToString(),
                Library = library,
                FilterName = "Oiii",
                Path = @"/tmp/image-Oiii.fits",
            },
        ]);

        dbContext.SaveChanges();
    }

    [Test]
    [Arguments("Ha")]
    [Arguments("ha")]
    [Arguments("HA")]
    public async Task Filter_Simple(string filterContent)
    {
        using var fixture = CreateDatabaseFixture();
        var filter = new FilterFilter { Filter = filterContent };
        var query = fixture.DbContext.ImageFiles.AsQueryable();
        query = filter.ApplyFilter(fixture.DbContext, query);

        var matches = query.ToList();

        await Assert.That(matches.Select(x => x.FilterName))
            .IsNotEmpty()
            .And
            .All(x => x == "Ha");
    }

    [Test]
    [Arguments("ha|sii|oiii")]
    [Arguments("Ha|Sii|Oiii")]
    [Arguments("HA|SII|OIII")]
    public async Task Filter_BooleanOr(string filterContent)
    {
        using var fixture = CreateDatabaseFixture();
        var filter = new FilterFilter { Filter = filterContent };
        var query = fixture.DbContext.ImageFiles.AsQueryable();
        query = filter.ApplyFilter(fixture.DbContext, query);

        var matches = query.ToList();

        await Assert.That(matches.Select(x => x.FilterName))
            .IsNotEmpty()
            .And
            .ContainsOnly(x => x == "Ha" || x == "Sii" || x == "Oiii");
    }
}
