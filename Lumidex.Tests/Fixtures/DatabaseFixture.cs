using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public LumidexDbContext DbContext { get; } = null!;

    public string DatabaseFilename { get; }

    // Exposed so tests can build their own fresh contexts against the same DB.
    public DbContextOptions<LumidexDbContext> Options { get; }

    public DatabaseFixture()
    {
        var tempFilename = $"lumidex-{Path.GetFileName(Path.GetTempFileName())}.db";
        DatabaseFilename = Path.Combine(Path.GetTempPath(), "lumidex", tempFilename);

        var builder = new DbContextOptionsBuilder<LumidexDbContext>();
        builder = builder.UseSqlite($"Data Source={DatabaseFilename}", config => config
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .EnableSensitiveDataLogging(false);

        Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFilename)!);
        Options = builder.Options;
        DbContext = new LumidexDbContext(Options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }
}
