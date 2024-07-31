using Flurl;
using Flurl.Http;
using Serilog;
using System.Text;

namespace Lumidex.Core.Exporters;

public class AstrobinAcquisitionExporter
{
    private static readonly string AstrobinFilterApiUrl = "https://app.astrobin.com/api/v2/equipment/filter/";

    public string ExportCsv(IReadOnlyList<AstrobinImageGroup> groups)
    {
        var exportItems = groups
            .Select(x => new AstrobinCsvExport
            {
                FilterId = x.Filter?.Id.ToString(),
                Number = x.Count.ToString(),
                Duration = x.Duration.TotalSeconds.ToString("F4"),
            });

        bool hasFilters = exportItems.Any(x => !string.IsNullOrWhiteSpace(x.FilterId));
        var sb = new StringBuilder();

        // Headers
        if (hasFilters)
        {
            sb.AppendLine("filter,number,duration");
        }
        else
        {
            sb.AppendLine("number,duration");
        }

        // Data
        foreach (var item in exportItems)
        {
            if (hasFilters)
            {
                sb.Append(item.FilterId);
                sb.Append(',');
            }

            sb.Append(item.Number);
            sb.Append(',');
            sb.Append(item.Duration);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task<IReadOnlyList<AstrobinFilter>> QueryFilters(string? filterName, CancellationToken token = default)
    {
        try
        {
            // Tried using a custom Lumidex user agent but Astrobin returns a 403.
            // Instead, use a Firefox user agent...
            var result = await AstrobinFilterApiUrl
                .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:128.0) Gecko/20100101 Firefox/128.0")
                .WithHeader("Accept", "application/json")
                .SetQueryParam("page", 1)
                .SetQueryParam("sort", "az")
                .SetQueryParam("q", filterName ?? string.Empty)
                .SetQueryParam("include-variants", true)
                .GetJsonAsync<AstrobinFilterDto>(cancellationToken: token);

            var filters = result.Results
                .Select(x => new AstrobinFilter
                {
                    Id = x.Id,
                    Name = x.BrandName + " " + x.Name,
                })
                .ToList();

            return filters;
        }
        catch (OperationCanceledException) { }
        catch (FlurlHttpException ex)
        {
            if (ex.InnerException is OperationCanceledException)
                return [];

            Log.Error(ex, "Error querying Astrobin for filter: {FilterName}", filterName);
            throw;
        }

        return [];
    }

    private class AstrobinCsvExport
    {
        public required string? FilterId { get; init; }
        public required string Number { get; init; }
        public required string Duration { get; init; }
    }

    private class AstrobinFilterDto
    {
        public int Count { get; set; }
        public List<AstrobinFilterResultDto> Results { get; set; } = [];
    }

    private class AstrobinFilterResultDto
    {
        public int Id { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}

public class AstrobinImageGroup
{
    public required int Count { get; init; }
    public required TimeSpan Duration { get; init; }
    public AstrobinFilter? Filter { get; init; }
}

public record AstrobinFilter
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}
