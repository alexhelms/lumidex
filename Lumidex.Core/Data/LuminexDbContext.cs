using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

public enum ImageType
{
    Unknown = 0,
    Light = 1,
    Flat = 2,
    Dark = 3,
    Bias = 4,
}

public enum ImageKind
{
    Unknown = 0,
    Raw = 1,
    Intermediate = 2,
    Calibration = 3,
    Master = 4,
    Processed = 5,
}

[Index(nameof(HeaderHash))]
public class ImageFile
{
    [Key]
    public int Id { get; set; }
    
    public string HeaderHash { get; set; } = string.Empty;
    
    public string Path { get; set; } = string.Empty;

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

    public ImageType Type { get; set; }
    
    public ImageKind Kind { get; set; }

    #region Camera

    public string? CameraName { get; set; }

    public TimeSpan? Exposure { get; set; }

    public double? CameraTemperatureSetPoint { get; set; }

    public double? CameraTemperature { get; set; }

    public int? CameraGain { get; set; }

    public int? CameraOffset { get; set; }

    public int? Binning { get; set; }

    public double? PixelSize { get; set; }

    public string? ReadoutMode { get; set; }

    #endregion

    #region Focuser 
 
    public string? FocuserName { get; set; }

    public int? FocuserPosition { get; set; }

    public double? FocuserTemperature { get; set; }

    #endregion

    #region Rotator

    public string? RotatorName { get; set; }

    public int? RotatorPosition { get; set; }

    #endregion

    #region Filter Wheel

    public string? FilterWheelName { get; set; }

    public string? FilterName { get; set; }

    #endregion

    #region Mount

    public string? MountName { get; set; }

    public double? RightAscension { get; set; }

    public double? Declination { get; set; }

    public double? Altitude { get; set; }

    public double? Azimuth { get; set; }

    #endregion

    #region Telescope

    public double? FocalLength { get; set; }

    public double? Airmass { get; set; }

    #endregion

    #region Target

    [Column(TypeName = "DATETIME")]
    public DateTime? ObservationTimestampUtc { get; set; }

    public string? ObjectName { get; set; }

    #endregion

    #region Site

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Elevation { get; set; }

    #endregion

    #region Weather

    public double? DewPoint { get; set; }

    public double? Humidity { get; set; }

    public double? Pressure { get; set; }

    public double? Temperature { get; set; }

    #endregion
}
