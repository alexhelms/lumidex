using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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
    public DbSet<AssociatedName> AssociatedNames { get; set; }

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
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
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

        modelBuilder.Entity<AssociatedName>()
            .Property(x => x.CreatedOn)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<Tag>()
            .Property(x => x.Color)
            .HasDefaultValue("#ffffffff");

        modelBuilder.Entity<ImageFile>()
            .HasMany(e => e.Tags)
            .WithMany();
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

    public IQueryable<ImageFile> SearchImageFilesQuery(ImageFileFilters filters, bool tracking)
    {
        IQueryable<ImageFile> query = ImageFiles.AsQueryable();

        if (tracking)
            query = query.AsNoTracking();

        if (filters.LibraryId.HasValue)
            query = query.Where(f => f.LibraryId == filters.LibraryId);

        if (filters.ObjectName is { Length: > 0 })
            query = query.Where(f => EF.Functions.Like(f.ObjectName, $"%{filters.ObjectName}%"));

        if (filters.ImageType is { } imageType)
            query = query.Where(f => f.Type == imageType);

        if (filters.ImageKind is { } imageKind)
            query = query.Where(f => f.Kind == imageKind);

        if (filters.ExposureMin is { } min)
            query = query.Where(f => f.Exposure!.Value >= min.TotalSeconds);

        if (filters.ExposureMax is { } max)
            query = query.Where(f => f.Exposure!.Value <= max.TotalSeconds);

        if (filters.Filter is { Length: > 0 } filter)
            query = query.Where(f => f.FilterName == filter);

        if (filters.DateBegin is { } dateBegin)
            query = query.Where(f => f.ObservationTimestampUtc >= dateBegin);

        if (filters.DateEnd is { } dateEnd)
            query = query.Where(f => f.ObservationTimestampUtc <= dateEnd);

        if (filters.TagIds is not null && filters.TagIds.Any())
        {
            var tagIds = filters.TagIds.ToHashSet();
            query = query.Where(f => f.Tags.Any(tag => tagIds.Contains(tag.Id)));
        }

        return query;
    }

    public List<ImageFile> SearchImageFiles(ImageFileFilters filters)
        => SearchImageFiles(filters, false);

    public List<ImageFile> SearchImageFiles(ImageFileFilters filters, bool tracking)
    {
        var query = SearchImageFilesQuery(filters, tracking);
        return query
            .Include(f => f.Library)
            .Include(f => f.Tags)
            .Include(f => f.AssociatedNames)
            .ToList();
    }

    public List<T> SearchImageFilesAndProject<T>(ImageFileFilters filters, Func<ImageFile, T> mapper)
        => SearchImageFilesAndProject<T>(filters, false, mapper);

    public List<T> SearchImageFilesAndProject<T>(ImageFileFilters filters, bool tracking, Func<ImageFile, T> mapper)
    {
        var query = SearchImageFilesQuery(filters, tracking);
        return query
            .Include(f => f.Library)
            .Include(f => f.Tags)
            .Include(f => f.AssociatedNames)
            .Select(mapper)
            .ToList();
    }

    public int AddTagToImageFiles(int tagId, IEnumerable<int> imageFileIds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tagId, 1);

        var tag = Tags.FirstOrDefault(tag => tag.Id == tagId);
        if (tag is not null)
        {
            var idLookup = imageFileIds.ToHashSet();
            var imageFiles = ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Add(tag);
            }

            return SaveChanges();
        }

        return 0;
    }

    public int AddTagsToImageFiles(IEnumerable<int> tagIds, IEnumerable<int> imageFileIds)
    {
        int count = 0;
        var tagIdLookup = tagIds.ToHashSet();
        var idLookup = imageFileIds.ToHashSet();

        var tags = Tags.Where(tag => tagIdLookup.Contains(tag.Id)).ToList();
        foreach (var tag in tags)
        {
            var imageFiles = ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Add(tag);
            }

            count += SaveChanges();
        }

        return count;
    }

    public int RemoveTagFromImageFiles(int tagId, IEnumerable<int> imageFileIds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tagId, 1);

        var tag = Tags.FirstOrDefault(tag => tag.Id == tagId);
        if (tag is not null)
        {
            var idLookup = imageFileIds.ToHashSet();
            var imageFiles = ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Remove(tag);
            }

            return SaveChanges();
        }

        return 0;
    }

    public int RemoveTagsFromImageFiles(IEnumerable<int> tagIds, IEnumerable<int> imageFileIds)
    {
        int count = 0;
        var tagIdLookup = tagIds.ToHashSet();
        var idLookup = imageFileIds.ToHashSet();

        var tags = Tags.Where(tag => tagIdLookup.Contains(tag.Id)).ToList();
        foreach (var tag in tags)
        {
            var imageFiles = ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

            foreach (var imageFile in imageFiles)
            {
                imageFile.Tags.Remove(tag);
            }

            count += SaveChanges();
        }

        return count;
    }

    public int ClearTagsFromImageFiles(IEnumerable<int> imageFileIds)
    {
        var idLookup = imageFileIds.ToHashSet();
        var imageFiles = ImageFiles
                .Include(f => f.Tags)
                .Where(f => idLookup.Contains(f.Id))
                .ToList();

        foreach (var imageFile in imageFiles)
        {
            imageFile.Tags.Clear();
        }

        return SaveChanges();
    }
}
