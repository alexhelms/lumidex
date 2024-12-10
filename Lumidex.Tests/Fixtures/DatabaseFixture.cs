using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public LumidexDbContext DbContext { get; } = null!;

    public string DatabaseFilename { get; }

    public DatabaseFixture()
    {
        var tempFilename = $"lumidex-{Path.GetFileName(Path.GetTempFileName())}.db";
        DatabaseFilename = Path.Combine(Path.GetTempPath(), "lumidex", tempFilename);

        var builder = new DbContextOptionsBuilder<LumidexDbContext>();
        builder = builder.UseSqlite($"Data Source={DatabaseFilename}", config => config
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .EnableSensitiveDataLogging(false);

        Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFilename)!);
        DbContext = new LumidexDbContext(builder.Options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }
}
