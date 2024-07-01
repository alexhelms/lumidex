using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO.Abstractions;

namespace Lumidex.Core.Data;

public class LumidexDbContextFactory : IDesignTimeDbContextFactory<LumidexDbContext>
{
    public LumidexDbContext CreateDbContext(string[] args)
    {
        return new LumidexDbContext(new FileSystem());
    }
}

public class LumidexDbContext : DbContext
{
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<Library> Libraries { get; set; }
    public DbSet<ImageFile> ImageFiles { get; set; }

    public string DbPath { get; }

    public LumidexDbContext(IFileSystem fileSystem)
    {
        DbPath = fileSystem.Path.Combine(LumidexPaths.AppData, "lumidex-data.db");
        fileSystem.Directory.CreateDirectory(LumidexPaths.AppData);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageFile>()
            .Property(x => x.CreatedOn)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
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
        var entitiesWithUpdatedOn = ChangeTracker.Entries().Where(c => c.State == EntityState.Modified);
        foreach (var entity in entitiesWithUpdatedOn)
        {
            if (entity.Properties.Any(c => c.Metadata.Name == nameof(ImageFile.UpdatedOn)))
            {
                entity.Property(nameof(ImageFile.UpdatedOn)).CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
