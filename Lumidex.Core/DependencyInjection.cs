using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Lumidex.Core.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO.Abstractions;

namespace Lumidex.Core;

public static class DependencyInjection
{
    public static void AddLumidexCore(this IServiceCollection services)
    {
        services.AddTransient<IFileSystem, FileSystem>();
        services.AddTransient<LibraryIngestPipeline>();
        services.AddTransient<Func<LibraryIngestPipeline>>(provider
            => () => provider.GetRequiredService<LibraryIngestPipeline>());
        
        services.AddDbContextFactory<LumidexDbContext>(options =>
        {
            string connectionString = string.Empty;

            if (Environment.GetEnvironmentVariable("LUMIDEX_TEST") is { Length: > 0 })
            {
                var tempFilename = $"lumidex-{Path.GetFileName(Path.GetTempFileName())}.db";
                var dbPath = Path.Combine(Path.GetTempPath(), "lumidex", tempFilename);
                connectionString = $"Data Source={dbPath}";
            }
            else
            {
                Directory.CreateDirectory(LumidexPaths.AppData);
                var dbPath = Path.Combine(LumidexPaths.AppData, "lumidex-data.db");
                connectionString = $"Data Source={dbPath}";
            }

            bool enableSensitiveLogging = false;

#if DEBUG
            //ILoggerFactory factory = new LoggerFactory().AddSerilog();
            //options.UseLoggerFactory(factory);
            //enableSensitiveLogging = true;
#endif
            options.UseSqlite(connectionString, config => config
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .EnableSensitiveDataLogging(enableSensitiveLogging);
        });
    }

    public static void UseLumidexCore(this IServiceProvider services)
    {
        var fileSystem = services.GetRequiredService<IFileSystem>();
        using var scope = services.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<LumidexDbContext>();
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
