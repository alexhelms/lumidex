using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO.Abstractions;

namespace Lumidex.Core.Data;

// Used by the ef tooling to create migrations.
public class LumidexDbContextFactory : IDesignTimeDbContextFactory<LumidexDbContext>
{
    public LumidexDbContext CreateDbContext(string[] args)
    {
        return new LumidexDbContext(new FileSystem());
    }
}

public class LumidexDbContext : DbContext
{
    private readonly IFileSystem _fileSystem;

    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<Library> Libraries { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ImageFile> ImageFiles { get; set; }
    public DbSet<ObjectAlias> ObjectAliases { get; set; }
    public DbSet<AstrobinFilter> AstrobinFilters { get; set; }

    public string DbPath { get; }

    public LumidexDbContext(
        IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        DbPath = fileSystem.Path.Combine(LumidexPaths.AppData, "lumidex-data.db");
        fileSystem.Directory.CreateDirectory(LumidexPaths.AppData);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
#if DEBUG
        //ILoggerFactory factory = new LoggerFactory().AddSerilog();
        //options.UseLoggerFactory(factory);
#endif

        options.UseSqlite($"Data Source={DbPath}", config => config
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .EnableSensitiveDataLogging(false);
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
