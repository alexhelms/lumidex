using Lumidex.Tests.Fixtures;
using Lumidex.Features.MainSearch.Filters;

namespace Lumidex.Tests;
public class SearchFilterTests : IClassFixture<DatabaseFixture>
{
    public DatabaseFixture Fixture { get; }

    public SearchFilterTests(DatabaseFixture fixture)
    {
        Fixture = fixture;
        SeedDatabase();
    }

    private void SeedDatabase()
    {
        var dbContext = Fixture.DbContext;

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

    [Theory]
    [InlineData("Ha")]
    [InlineData("ha")]
    [InlineData("HA")]
    public void Filter_Simple(string filterContent)
    {
        var filter = new FilterFilter { Filter = filterContent };
        var query = Fixture.DbContext.ImageFiles.AsQueryable();
        query = filter.ApplyFilter(Fixture.DbContext, query);

        var matches = query.ToList();
        
        matches
            .Select(x => x.FilterName)
            .Should()
            .NotBeEmpty()
            .And
            .AllBe("Ha");
    }

    [Theory]
    [InlineData("ha|sii|oiii")]
    [InlineData("Ha|Sii|Oiii")]
    [InlineData("HA|SII|OIII")]
    public void Filter_BooleanOr(string filterContent)
    {
        var filter = new FilterFilter { Filter = filterContent };
        var query = Fixture.DbContext.ImageFiles.AsQueryable();
        query = filter.ApplyFilter(Fixture.DbContext, query);

        var matches = query.ToList();

        matches
            .Select(x => x.FilterName)
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(s => s == "Ha" || s == "Sii" || s == "Oiii");
    }
}
