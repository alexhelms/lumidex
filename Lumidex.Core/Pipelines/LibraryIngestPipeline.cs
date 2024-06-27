using FluentResults;
using Lumidex.Core.Data;
using Lumidex.Core.Detection;
using Lumidex.Core.IO;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;

namespace Lumidex.Core.Pipelines;

public record IngestProgress
{
    public int AddedCount { get; init; }
    public int SkipCount { get; init; }
    public int ErrorCount { get; init; }
}

public class LibraryIngestPipeline
{
    private readonly IFileSystem _fileSystem;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    private int _addedCount;
    private int _skippedCount;
    private int _errorCount;

    private record HashedImage(IFileInfo FileInfo, string Hash);

    public ConcurrentBag<IFileInfo> HashFailures { get; private set; } = new();

    public ConcurrentBag<IFileInfo> HeaderParseFailures { get; private set; } = new();

    public int AddedCount => _addedCount;
    public int SkippedCount => _skippedCount;
    public int ErrorCount => _errorCount;
    public int TotalCount => AddedCount + SkippedCount + ErrorCount;

    public LibraryIngestPipeline(
        IFileSystem fileSystem,
        IDbContextFactory<LumidexDbContext> dbContextFactory)
    {
        _fileSystem = fileSystem;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Get an estimated count of files to be processed.
    /// 
    /// This count is the number of files with the supported extensions.
    /// Some files may not added to the database for various reasons.
    /// This estimate is intended for progress bars.
    /// </summary>
    /// <param name="rootDirectory">The root directory to scan.</param>
    /// <returns>An estimated count of files to be processed.</returns>
    public Task<int> GetEstimatedTotalFiles(string rootDirectory) 
        => Task.Run(() => DirectoryWalker.Walk(_fileSystem, rootDirectory).Count());

    public async Task ProcessAsync(string rootDirectory, IProgress<IngestProgress>? progress = null, CancellationToken token = default)
    {
        HashFailures = new();
        HeaderParseFailures = new();

        var internalProgress = new Progress<IngestProgress>(p =>
        {
            Interlocked.Add(ref _addedCount, p.AddedCount);
            Interlocked.Add(ref _skippedCount, p.SkipCount);
            Interlocked.Add(ref _errorCount, p.ErrorCount);
            progress?.Report(p);
        });

        var (pipeline, completion) = CreatePipeline(internalProgress);

        Log.Information("Starting library ingest pipeline...");
        var start = Stopwatch.GetTimestamp();
        foreach (var fileInfo in DirectoryWalker.Walk(_fileSystem, rootDirectory))
        {
            pipeline.Post(fileInfo);
        }

        pipeline.Complete();
        await pipeline.Completion.WaitAsync(token);
        await completion;

        var elapsed = Stopwatch.GetElapsedTime(start);
        Log.Information("Library ingest completed in {Elapsed:F3} seconds", elapsed.TotalSeconds);
        Log.Information("{AddedCount} added, {SkippedCount} skipped, {ErrorCount} errors", AddedCount, SkippedCount, ErrorCount);

        if (HashFailures.Count > 0)
        {
            Log.Warning("{Count} hash failures, listed below");
            foreach (var failure in HashFailures)
            {
                Log.Warning("Hash failure: {Filename}", failure.FullName);
            }
        }

        if (HeaderParseFailures.Count > 0)
        {
            Log.Warning("{Count} header parse failures, listed below");
            foreach (var failure in HeaderParseFailures)
            {
                Log.Warning("Header parse failure: {Filename}", failure.FullName);
            }
        }
    }

    private (ITargetBlock<IFileInfo>, Task) CreatePipeline(IProgress<IngestProgress>? progress = null)
    {
        // HELPER BLOCKS: Progress etc.
        var addedProgressBlock = new ActionBlock<int>(count => progress?.Report(new() { AddedCount = count }));
        var skippedProgressBlock = new ActionBlock<int>(count => progress?.Report(new() { SkipCount = count }));
        var errorProgressBlock = new ActionBlock<int>(count => progress?.Report(new() { ErrorCount = count }));

        // STEP 1: Hash the header and only process the file if the hash is not in the database.
        var hashBlock = new TransformBlock<IFileInfo, Result<HashedImage>>(async fileInfo =>
        {
            try
            {
                var hasher = new FitsHeaderHasher();
                var hash = await hasher.ComputeHashAsync(fileInfo.FullName);
                var hashString = string.Concat(hash.Select(h => h.ToString("x2")));
                return Result.Ok(new HashedImage(fileInfo, hashString));
            }
            catch (Exception ex)
            {
                await errorProgressBlock.SendAsync(1);
                HashFailures.Add(fileInfo);
                Log.Error(ex, "Error hashing {Filename}", fileInfo.FullName);
                return Result.Fail(new Error("Error hashing file"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            SingleProducerConstrained = true,
        });

        // STEP 2: Extract data from the image header and create the database object.
        var createImageFileBlock = new TransformBlock<Result<HashedImage>, Result<ImageFile>>(async result =>
        {
            try
            {
                var reader = new HeaderReader();
                var imageFile = reader.Process(result.Value.FileInfo, result.Value.Hash);
                return Result.Ok(imageFile);
            }
            catch (Exception ex)
            {
                await errorProgressBlock.SendAsync(1);
                HeaderParseFailures.Add(result.Value.FileInfo);
                Log.Error(ex, "Error processing image header in {Filename}", result.Value.FileInfo.FullName);
                return Result.Fail(new Error("Error processing image header"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        });

        // STEP 3: Batch new database objects.
        var addToDatabaseBatchBlock = new BatchBlock<Result<ImageFile>>(100);

        // STEP 4: Save new objects in database.
        var addToDatabaseBlock = new ActionBlock<Result<ImageFile>[]>(async results =>
        {
            var imageFiles = results
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

            if (imageFiles.Count > 0)
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                await dbContext.ImageFiles.AddRangeAsync(imageFiles);
                await dbContext.SaveChangesAsync();
                await addedProgressBlock.SendAsync(imageFiles.Count);
            }
        }, new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
        });

        // Build the pipline
        var options = new DataflowLinkOptions { PropagateCompletion = true };

        hashBlock.LinkTo(createImageFileBlock, options,
            result =>
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                bool isInDatabase = dbContext.ImageFiles.Any(imageFile => imageFile.HeaderHash == result.Value.Hash);
                return isInDatabase == false;
            });
        hashBlock.LinkTo(new ActionBlock<Result<HashedImage>>(async _ => await skippedProgressBlock.SendAsync(1)), options);

        createImageFileBlock.LinkTo(addToDatabaseBatchBlock, options, result => result.IsSuccess);
        createImageFileBlock.LinkTo(DataflowBlock.NullTarget<Result<ImageFile>>(), options);

        addToDatabaseBatchBlock.LinkTo(addToDatabaseBlock, options);

        var completion = addToDatabaseBlock.Completion
            .ContinueWith(t =>
            {
                addedProgressBlock.Complete();
            });

        return (hashBlock, completion);
    }
}
