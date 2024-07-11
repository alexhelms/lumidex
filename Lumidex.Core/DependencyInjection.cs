using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Lumidex.Core.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;

namespace Lumidex.Core;

public static class DependencyInjection
{
    public static void AddLumidexCore(this IServiceCollection services)
    {
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddTransient<LibraryIngestPipeline>();
        services.AddTransient<Func<LibraryIngestPipeline>>(provider => () => provider.GetRequiredService<LibraryIngestPipeline>());
        
        services.AddDbContextFactory<LumidexDbContext>();
    }

    public static void UseLumidexCore(this IServiceProvider services)
    {
        var fileSystem = services.GetRequiredService<IFileSystem>();
        using var scope = services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<LumidexDbContext>();
        dbContext.Database.EnsureCreated();
        if (dbContext.Database.GetPendingMigrations().Any())
        {
            dbContext.Database.Migrate();
        }

        // Create the initial appsettings row
        if (dbContext.AppSettings.Count() == 0)
        {
            var appSettings = new AppSettings();
            dbContext.AppSettings.Add(appSettings);
            dbContext.SaveChanges();
        }

        // Create the default library
        if (dbContext.Libraries.Count() == 0)
        {
            fileSystem.Directory.CreateDirectory(LumidexPaths.DefaultLibrary);
            dbContext.Libraries.Add(new Library
            {
                Name = "Default",
                Path = LumidexPaths.DefaultLibrary,
            });
            dbContext.SaveChanges();
        }
    }
}
