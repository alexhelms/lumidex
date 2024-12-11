using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lumidex.Core.Data;

// Used by the ef tooling to create migrations.
public class LumidexDbContextFactory : IDesignTimeDbContextFactory<LumidexDbContext>
{
    public LumidexDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(LumidexPaths.AppData, "lumidex-data.db");
        var builder = new DbContextOptionsBuilder<LumidexDbContext>();
        builder = builder.UseSqlite($"Data Source={dbPath}", config => config
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .EnableSensitiveDataLogging(false);
        return new LumidexDbContext(builder.Options);
    }
}

public class LumidexDbContext : DbContext
{
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<Library> Libraries { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ImageFile> ImageFiles { get; set; }
    public DbSet<ObjectAlias> ObjectAliases { get; set; }
    public DbSet<AstrobinFilter> AstrobinFilters { get; set; }

    public LumidexDbContext(DbContextOptions<LumidexDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageFile>()
            .Property(x => x.CreatedOn)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<Library>()
            .Property(x => x.CreatedOn)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<Tag>()
            .Property(x => x.CreatedOn)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<Tag>()
            .Property(x => x.Color)
            .HasDefaultValue("#ffffffff");
            
    }

    public override int SaveChanges()
    {
        ModifyUpdatedOnColumn();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ModifyUpdatedOnColumn();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ModifyUpdatedOnColumn();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ModifyUpdatedOnColumn();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Automatically update the "UpdatedOn" time stamp on all modified entities.
    /// </summary>
    private void ModifyUpdatedOnColumn()
    {
        var addedEntities = ChangeTracker.Entries().Where(c => c.State == EntityState.Added);
        foreach (var entity in addedEntities)
        {
            if (entity.Properties.Any(c => c.Metadata.Name == nameof(ImageFile.CreatedOn)))
            {
                entity.Property(nameof(ImageFile.CreatedOn)).CurrentValue = DateTime.UtcNow;
            }
        }

        var modifiedEntities = ChangeTracker.Entries().Where(c => c.State == EntityState.Modified);
        foreach (var entity in modifiedEntities)
        {
            if (entity.Properties.Any(c => c.Metadata.Name == nameof(ImageFile.UpdatedOn)))
            {
                entity.Property(nameof(ImageFile.UpdatedOn)).CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
