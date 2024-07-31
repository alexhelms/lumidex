using Flurl.Http.Testing;
using Lumidex.Core.Data;
using Lumidex.Core.Exporters;
using AstrobinFilter = Lumidex.Core.Exporters.AstrobinFilter;

namespace Lumidex.Tests;

public partial class AstrobinExportTests : IDisposable
{
    private readonly HttpTest _httpTest = new();

    public void Dispose()
    {
        _httpTest.Dispose();
    }

    [Fact]
    public void Export_WithFilters()
    {
        var items = new List<AstrobinImageGroup>
        {
            new AstrobinImageGroup
            {
                Count = 3,
                Duration = TimeSpan.FromSeconds(60),
                Filter = new AstrobinFilter
                {
                    Id = 1,
                    Name = "Antlia L"
                }
            },
            new AstrobinImageGroup
            {
                Count = 5,
                Duration = TimeSpan.FromSeconds(120),
                Filter = new AstrobinFilter
                {
                    Id = 1,
                    Name = "Antlia L"
                }
            },
            new AstrobinImageGroup
            {
                Count = 4,
                Duration = TimeSpan.FromSeconds(10),
                Filter = new AstrobinFilter
                {
                    Id = 2,
                    Name = "Antlia R"
                }
            },
            new AstrobinImageGroup
            {
                Count = 5,
                Duration = TimeSpan.FromSeconds(20),
                Filter = new AstrobinFilter
                {
                    Id = 3,
                    Name = "Antlia G"
                }
            },
            new AstrobinImageGroup
            {
                Count = 6,
                Duration = TimeSpan.FromSeconds(30),
                Filter = new AstrobinFilter
                {
                    Id = 4,
                    Name = "Antlia B"
                }
            },
        };

        var exporter = new AstrobinAcquisitionExporter();
        var csv = exporter.ExportCsv(items);
        var expected =
            """
            filter,number,duration
            1,3,60.0000
            1,5,120.0000
            2,4,10.0000
            3,5,20.0000
            4,6,30.0000

            """;
        csv.Should().Be(expected);
    }

    [Fact]
    public void Export_WithoutFilters()
    {
        var items = new List<AstrobinImageGroup>
        {
            new AstrobinImageGroup
            {
                Count = 3,
                Duration = TimeSpan.FromSeconds(60),
            }
        };

        var exporter = new AstrobinAcquisitionExporter();
        var csv = exporter.ExportCsv(items);
        var expected =
            """
            number,duration
            3,60.0000

            """;
        csv.Should().Be(expected);
    }

    [Fact]
    public void Export_WithAndWithoutFilters()
    {
        var items = new List<AstrobinImageGroup>
        {
            new AstrobinImageGroup
            {
                Count = 3,
                Duration = TimeSpan.FromSeconds(60),
                Filter = new AstrobinFilter
                {
                    Id = 1,
                    Name = "Antlia L"
                }
            },
            new AstrobinImageGroup
            {
                Count = 4,
                Duration = TimeSpan.FromSeconds(120),
            }
        };

        var exporter = new AstrobinAcquisitionExporter();
        var csv = exporter.ExportCsv(items);
        var expected =
            """
            filter,number,duration
            1,3,60.0000
            ,4,120.0000

            """;
        csv.Should().Be(expected);
    }

    [Fact]
    public async Task QueryFilters()
    {
        var query = "antlia green 2\"";
        _httpTest
            .ForCallsTo("https://app.astrobin.com/api/v2/equipment/filter/")
            .RespondWith(AstrobinFilterResponseJson);

        var exporter = new AstrobinAcquisitionExporter();
        var filters = await exporter.QueryFilters(query);
        filters.Should().HaveCount(50);

        // Spot check
        filters[0].Id.Should().Be(4422);
        filters[0].Name.Should().Be("Antlia Green 2\"");

        _httpTest
            .ShouldHaveCalled("https://app.astrobin.com/api/v2/equipment/filter/")
            .WithVerb(HttpMethod.Get)
            .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0")
            .WithHeader("Accept", "application/json")
            .WithQueryParam("page", 1)
            .WithQueryParam("sort", "az")
            .WithQueryParam("q", query)
            .WithQueryParam("include-variants", true)
            .Times(1);

    }
}
