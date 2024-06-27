using Lumidex.Core;
using Lumidex.Core.Data;
using Lumidex.Core.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

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
await dbContext.Database.EnsureCreatedAsync();

var ingest = services.GetRequiredService<LibraryIngestPipeline>();
await ingest.ProcessAsync(rootDir);

await Log.CloseAndFlushAsync();