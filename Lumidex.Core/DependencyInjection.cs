using Lumidex.Core.Data;
using Lumidex.Core.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;

namespace Lumidex.Core;

public static class DependencyInjection
{
    public static void AddLumidexCore(this IServiceCollection services)
    {
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddDbContextFactory<LumidexDbContext>();
        services.AddTransient<LibraryIngestPipeline>();
    }
}
