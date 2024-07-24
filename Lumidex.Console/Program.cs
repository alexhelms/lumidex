using Lumidex.Core;
using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Lumidex.Core.Pipelines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

string libraryName = "default";
string rootDir = @"C:\tmp\lumidex";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Async(wt => wt.Console())
    .Enrich.FromLogContext()
    .CreateLogger();

var collection = new ServiceCollection();
collection.AddLumidexCore();
IServiceProvider services = collection.BuildServiceProvider();

var dbContext = services.GetRequiredService<LumidexDbContext>();
await dbContext.Database.MigrateAsync();

if (dbContext.Libraries.Count() == 0)
{
    dbContext.Libraries.Add(new Library
    {
        Name = libraryName,
        Path = rootDir,
    });
    await dbContext.SaveChangesAsync();
}

var library = await dbContext.Libraries.SingleAsync(l => l.Name == libraryName);
var ingest = services.GetRequiredService<LibraryIngestPipeline>();
await ingest.ProcessAsync(library, forceFullScan: true);

await Log.CloseAndFlushAsync();