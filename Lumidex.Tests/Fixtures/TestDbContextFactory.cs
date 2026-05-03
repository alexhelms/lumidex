using Lumidex.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Lumidex.Tests.Fixtures;

// Test-only IDbContextFactory that creates fresh LumidexDbContext instances
// pointing at the SQLite file owned by DatabaseFixture. LibraryIngestPipeline
// needs a factory (rather than a shared DbContext) so each pipeline stage can
// use its own short-lived context without cross-thread EF state interference.
internal sealed class TestDbContextFactory(string databaseFilename)
    : IDbContextFactory<LumidexDbContext>
{
    private readonly DbContextOptions<LumidexDbContext> _options =
        new DbContextOptionsBuilder<LumidexDbContext>()
            .UseSqlite($"Data Source={databaseFilename}")
            .Options;

    public LumidexDbContext CreateDbContext() => new LumidexDbContext(_options);
}
