using FluentResults;
using Lumidex.Core.Data;
using Lumidex.Core.Detection;
using Lumidex.Core.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks.Dataflow;

namespace Lumidex.Core.Pipelines;

public record IngestStatus(IFileInfo FileInfo, string Message);

public class IngestProgress
{
    public List<IngestStatus> Added { get; init; } = new();
    public List<IngestStatus> Skipped { get; init; } = new();
    public List<IngestStatus> Errors { get; init; } = new();
    public int TotalCount => Added.Count + Skipped.Count + Errors.Count;
}

public class LibraryIngestPipeline
{
    private readonly IFileSystem _fileSystem;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    private record FileMessage(IFileInfo FileInfo, int LibraryId);
    private record HashedImage(IFileInfo FileInfo, int LibraryId, string Hash);

    public ConcurrentBag<IngestStatus> Added { get; private set; } = new();
    public ConcurrentBag<IngestStatus> Skipped { get; private set; } = new();
    public ConcurrentBag<IngestStatus> Errors { get; private set; } = new();
    public int TotalCount => Added.Count + Skipped.Count + Errors.Count;

    public TimeSpan Elapsed { get; private set; }

    public LibraryIngestPipeline(
        IFileSystem fileSystem,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _fileSystem = fileSystem;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Get an estimated count of files to be processed since the last scan date.
    /// 
    /// If the library has never been scanned, all files regardless of age are scanned.
    /// 
    /// This count is the number of files with the supported extensions.
    /// Some files may not added to the database for various reasons.
    /// This estimate is intended for progress bars.
    /// </summary>
    /// <param name="library">The library to scan.</param>
    /// <returns>An estimated count of files to be processed.</returns>
    public Task<int> GetEstimatedTotalFiles(Library library, bool forceFullScan = false) => Task.Run(() =>
    {
        DateTime? startDate = forceFullScan ? null : library.LastScan;

        return DirectoryWalker.Walk(_fileSystem, library.Path, startDate).Count();
    });

    /// <summary>
    /// Scan the library for new image files starting from the last scan date.
    /// 
    /// If the library has never been scanned, all files regardless of age are scanned.
    /// </summary>
    /// <param name="library">The library to scan.</param>
    /// <param name="forceFullScan">Ignore the last scan date and force a full scan.</param>
    /// <param name="progress">Progress.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ProcessAsync(
        Library library,
        bool forceFullScan = false,
        IProgress<IngestProgress>? progress = null,
        CancellationToken token = default)
    {
        Added = new();
        Skipped = new();
        Errors = new();

        var internalProgress = new Progress<IngestProgress>(p =>
        {
            foreach (var item in p.Added)
                Added.Add(item);

            foreach (var item in p.Skipped)
                Skipped.Add(item);

            foreach (var item in p.Errors)
                Errors.Add(item);

            progress?.Report(p);
        });

        var (pipeline, completion) = CreatePipeline(internalProgress);

        Log.Information("Starting library ingest pipeline...");
        var start = Stopwatch.GetTimestamp();
        
        // Walk the directories on a background thread
        await Task.Run(async () =>
        {
            DateTime? startDate = forceFullScan ? null : library.LastScan;
            foreach (var fileInfo in DirectoryWalker.Walk(_fileSystem, library.Path, startDate))
            {
                await pipeline.SendAsync(new FileMessage(fileInfo, library.Id));
            }
        });

        pipeline.Complete();

        try
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            await dbContext.Libraries
                .Where(l => l.Id == library.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(l => l.LastScan, DateTime.UtcNow));

            await pipeline.Completion.WaitAsync(token);
            await completion;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Ingest canceled");
        }
        catch (Exception e)
        {
            Log.Error(e, "Error processing library");
        }

        Elapsed = Stopwatch.GetElapsedTime(start);
        Log.Information("Library ingest completed in {Elapsed:F3} seconds", Elapsed.TotalSeconds);
        Log.Information("{AddedCount} added, {SkippedCount} skipped, {ErrorCount} errors", Added.Count, Skipped.Count, Errors.Count);

        if (Errors.Count > 0)
        {
            foreach (var error in Errors)
            {
                Log.Warning("{Message}: {Filename}", error.Message, error.FileInfo.FullName);
            }
        }
    }

    private (ITargetBlock<FileMessage>, Task) CreatePipeline(IProgress<IngestProgress>? progress = null, CancellationToken token = default)
    {
        // HELPER BLOCKS: Progress etc.
        var addedProgressBlock = new ActionBlock<List<IngestStatus>>(
            status => progress?.Report(new() { Added = status })
        );
        var skippedProgressBlock = new ActionBlock<IngestStatus>(
            status => progress?.Report(new() { Skipped = [status] })
        );
        var errorProgressBlock = new ActionBlock<IngestStatus>(
            status => progress?.Report(new() { Errors = [status] })
        );

        // STEP 1: Hash the header and only process the file if the hash is not in the database.
        var hashBlock = new TransformBlock<FileMessage, Result<HashedImage>>(async message =>
        {
            try
            {
                var extension = message.FileInfo.Extension.ToLowerInvariant();
                HeaderHasher hasher = extension == ".xisf" ? new XisfHeaderHasher() : new FitsHeaderHasher();
                var hash = await hasher.ComputeHashAsync(message.FileInfo.FullName);
                var hashString = string.Concat(hash.Select(h => h.ToString("x2")));
                return Result.Ok(new HashedImage(message.FileInfo, message.LibraryId, hashString));
            }
            catch (Exception ex)
            {
                await errorProgressBlock.SendAsync(new(message.FileInfo, "Hash Error"));
                Log.Error(ex, "Error hashing {Filename}", message.FileInfo.FullName);
                return Result.Fail(new PipelineError(message.FileInfo, "Error hashing file"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            SingleProducerConstrained = true,
            CancellationToken = token,
        });

        // STEP 2: Extract data from the image header and create the database object.
        var createImageFileBlock = new TransformBlock<Result<HashedImage>, Result<(IFileInfo, ImageFile)>>(async result =>
        {
            try
            {
                var reader = new HeaderReader();
                var imageFile = reader.Process(result.Value.FileInfo, result.Value.Hash);
                imageFile.LibraryId = result.Value.LibraryId;
                return Result.Ok((result.Value.FileInfo, imageFile));
            }
            catch (Exception ex)
            {
                await errorProgressBlock.SendAsync(new(result.Value.FileInfo, "Header Error"));
                Log.Error(ex, "Error processing image header in {Filename}", result.Value.FileInfo.FullName);
                return Result.Fail(new PipelineError(result.Value.FileInfo, "Error processing image header"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token,
        });

        // STEP 3: Batch objects to be added to the database.
        var addToDatabaseBatchBlock = new BatchBlock<Result<(IFileInfo, ImageFile)>>(
            100,
            new GroupingDataflowBlockOptions()
            {
                CancellationToken = token,
            });

        // STEP 4: Save new objects in database.
        var addToDatabaseBlock = new ActionBlock<Result<(IFileInfo, ImageFile)>[]>(async results =>
        {
            var successes = results
                .Where(result => result.IsSuccess)
                .Select( result => result.Value)
                .ToList();

            var failures = results
                .Where(result => result.IsFailed)
                .Select( result => result.Value);

            if (failures.Any())
            {
                // This should never happen because the pipeline is setup to filter these before this block.
                Log.Error("Failed images made it to the database block");
            }

            if (successes.Count > 0)
            {
                var fileInfos = successes.Select(x => x.Item1);
                var imageFiles = successes.Select(x => x.Item2);
                var comparer = StringComparer.InvariantCultureIgnoreCase;
                Dictionary<string, AlternateName> alternateNames = new();

                try
                {
                    using var dbContext = _dbContextFactory.CreateDbContext();

                    // Create a lookup with all existing alternate names and any potential new ones
                    alternateNames = dbContext
                        .AlternateNames
                        .ToList()
                        .Concat(imageFiles
                            .Where(f => f.ObjectName != null)
                            .Select(f => f.ObjectName)
                            .Select(name => new AlternateName
                            {
                                Name = name!,
                            }))
                        .DistinctBy(alt => alt.Name, comparer)
                        .ToDictionary(alt => alt.Name, alt => alt, comparer);

                    // Add any new alternate names
                    dbContext.AlternateNames.AddRange(
                        alternateNames.Values.Where(alt => alt.Id == 0));

                    // Apply the alternate names to the image files
                    foreach (var imageFile in imageFiles.Where(f => f.ObjectName != null))
                    {
                        if (alternateNames.TryGetValue(imageFile.ObjectName!, out var alternateName))
                        {
                            imageFile.AlternateNames.Add(alternateName);
                        }
                    }

                    dbContext.ImageFiles.AddRange(imageFiles);
                    dbContext.SaveChanges();
                    await addedProgressBlock.SendAsync(
                        fileInfos
                            .Select(fileInfo => new IngestStatus(fileInfo, "Added"))
                            .ToList()
                    );
                }
                catch (DbUpdateException de) when (de.InnerException is SqliteException sle && sle.SqliteErrorCode == 19)
                {
                    Log.Error(de, "Foreign key constraint failed");
                    
                    Log.Information("Alternate names in database and new names being added:");
                    foreach (var item in alternateNames)
                    {
                        Log.Information("Id: {Id}, Name: {Name}", item.Value.Id, item.Key);
                    }

                    Log.Information("Files in batch:");
                    foreach(var item in imageFiles)
                    {
                        Log.Information("Filename: {Filename}", item.Path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adding images to database");
                }
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
            CancellationToken = token,
        });

        // Build the pipline
        var options = new DataflowLinkOptions { PropagateCompletion = true };

        hashBlock.LinkTo(createImageFileBlock, options,
            result =>
            {
                if (result.IsFailed)
                    return false;

                using var dbContext = _dbContextFactory.CreateDbContext();
                bool isInDatabase = dbContext.ImageFiles.Any(imageFile => imageFile.HeaderHash == result.Value.Hash);
                return isInDatabase == false;
            });
        hashBlock.LinkTo(new ActionBlock<Result<HashedImage>>(async result =>
        {
            IFileInfo? fileInfo = result.ValueOrDefault?.FileInfo;

            if (result.IsFailed)
            {
                if (result.Errors.OfType<PipelineError>().FirstOrDefault() is { } error)
                {
                    fileInfo = error.FileInfo;
                }
            }

            if (fileInfo is not null)
                await skippedProgressBlock.SendAsync(new(fileInfo, "Skipped"));

        }), options);

        createImageFileBlock.LinkTo(addToDatabaseBatchBlock, options, result => result.IsSuccess);

        addToDatabaseBatchBlock.LinkTo(addToDatabaseBlock, options);

        var completion = addToDatabaseBlock.Completion
            .ContinueWith(t =>
            {
                addedProgressBlock.Complete();
            });

        return (hashBlock, completion);
    }

    private class PipelineError : FluentResults.Error
    {
        public IFileInfo FileInfo { get; }

        public PipelineError(IFileInfo fileInfo, string message)
            : base(message)
        {
            FileInfo = fileInfo;
        }
    }
}
