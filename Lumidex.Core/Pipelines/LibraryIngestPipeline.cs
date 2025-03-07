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

public record IngestStatus(IFileInfo FileInfo, string Message);

public class IngestProgress
{
    public List<IngestStatus> Added { get; init; } = new();
    public List<IngestStatus> Updated { get; init; } = new();
    public List<IngestStatus> Skipped { get; init; } = new();
    public List<IngestStatus> Errors { get; init; } = new();
    public int TotalCount => Added.Count + Updated.Count + Skipped.Count + Errors.Count;
}

public class LibraryIngestPipeline
{
    private readonly IFileSystem _fileSystem;
    private readonly IDbContextFactory<LumidexDbContext> _dbContextFactory;

    public ConcurrentBag<IngestStatus> Added { get; private set; } = new();
    public ConcurrentBag<IngestStatus> Updated { get; private set; } = new();
    public ConcurrentBag<IngestStatus> Skipped { get; private set; } = new();
    public ConcurrentBag<IngestStatus> Errors { get; private set; } = new();
    public int TotalCount => Added.Count + Updated.Count + Skipped.Count + Errors.Count;
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
        Updated = new();
        Skipped = new();
        Errors = new();

        var internalProgress = new Progress<IngestProgress>(p =>
        {
            foreach (var item in p.Added)
                Added.Add(item);

            foreach (var item in p.Updated)
                Updated.Add(item);

            foreach (var item in p.Skipped)
                Skipped.Add(item);

            foreach (var item in p.Errors)
                Errors.Add(item);

            progress?.Report(p);
        });

        var (pipeline, completion) = CreatePipeline(internalProgress, token);

        Log.Information("Starting library ingest pipeline for {Path}", library.Path);
        var start = Stopwatch.GetTimestamp();

        // Walk the directories on a background thread
        await Task.Run(() =>
        {
            DateTime? startDate = forceFullScan ? null : library.LastScan;
            foreach (var fileInfo in DirectoryWalker.Walk(_fileSystem, library.Path, startDate))
            {
                pipeline.Post(new FileMessage(fileInfo, library.Id));
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
        Log.Information("{AddedCount} added, {UpdatedCount} updated, {SkippedCount} skipped, {ErrorCount} errors",
            Added.Count, Updated.Count, Skipped.Count, Errors.Count);

        if (Errors.Count > 0)
        {
            foreach (var error in Errors)
            {
                Log.Warning("{Message}: {Filename}", error.Message, error.FileInfo.FullName);
            }
        }
    }

    private (ITargetBlock<FileMessage>, Task) CreatePipeline(
        IProgress<IngestProgress>? progress = null,
        CancellationToken token = default)
    {
        // HELPER BLOCKS: Progress etc.
        var addedProgressBlock = new ActionBlock<List<IngestStatus>>(
            status => progress?.Report(new() { Added = status })
        );
        var updatedProgressBlock = new ActionBlock<List<IngestStatus>>(
            status => progress?.Report(new() { Updated = status })
        );
        var skippedProgressBlock = new ActionBlock<List<IngestStatus>>(
            status => progress?.Report(new() { Skipped = status })
        );
        var errorProgressBlock = new ActionBlock<List<IngestStatus>>(
            status => progress?.Report(new() { Errors = status })
        );

        // *******************************
        // STEP 1: Compute the header hash
        // *******************************
        var block1ComputeHash = new TransformBlock<FileMessage, Result<HashMessage>>(async message =>
        {
            try
            {
                HeaderHasher hasher = HeaderHasher.FromExtension(message.FileInfo.Extension);
                byte[] hash = await hasher.ComputeHashAsync(message.FileInfo.FullName);
                string headerHash = string.Concat(hash.Select(h => h.ToString("x2")));
                return Result.Ok(new HashMessage(message.FileInfo, message.LibraryId, headerHash));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error hashing {Filename}", message.FileInfo.FullName);
                errorProgressBlock.Post([new(message.FileInfo, "Hash Error")]);
                return Result.Fail(new PipelineError(message.FileInfo, "Error hashing file"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token,
        });

        // ***************************
        // STEP 2: Batch header hashes
        // ***************************
        var block2BatchHashes = new BatchBlock<Result<HashMessage>>(100,
            new GroupingDataflowBlockOptions
            {
                CancellationToken = token,
            });

        // ***********************************
        // STEP 3: Filter existing image files
        // ***********************************
        var block3GetOrCreateEntity = new TransformManyBlock<Result<HashMessage>[], Result<HashMessage>>(messages =>
        {
            HashSet<string> existingHashes = [];

            // Map hash -> file info
            var fileInfoLookup = messages
                .Select(m => m.Value)
                .DistinctBy(m => m.HeaderHash)
                .ToDictionary(m => m.HeaderHash, m => m.FileInfo);

            // Get existing image files and update non-header database columns
            using (var dbContext = _dbContextFactory.CreateDbContext())
            {
                var updateStatuses = new List<IngestStatus>(messages.Length);
                var skipStatuses = new List<IngestStatus>(messages.Length);
                var hashes = fileInfoLookup.Keys.ToHashSet();
                var imageFiles = dbContext.ImageFiles
                    .Where(f => hashes.Contains(f.HeaderHash))
                    .ToList();

                foreach (var imageFile in imageFiles)
                {
                    var fileInfo = fileInfoLookup[imageFile.HeaderHash];
                    if (UpdateNonHeaderFields(fileInfo, imageFile))
                    {
                        updateStatuses.Add(new IngestStatus(fileInfo, "Updated"));
                    }
                    else
                    {
                        skipStatuses.Add(new IngestStatus(fileInfo, "Skipped"));
                    }
                }

                int count = dbContext.SaveChanges();
                updatedProgressBlock.Post(updateStatuses);
                skippedProgressBlock.Post(skipStatuses);
                existingHashes = imageFiles
                    .Select(f => f.HeaderHash + "|" + f.Path)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            }

            return messages.Where(m => !existingHashes.Contains(m.Value.HeaderHash + "|" + m.Value.FileInfo.FullName));

        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token,
        });

        // **************************
        // STEP 4: Parse image header
        // **************************
        var block4ParseHeader = new TransformBlock<Result<HashMessage>, Result<EntityMessage>>(result =>
        {
            var (fileInfo, libraryId, headerHash) = result.Value;
            try
            {
                var reader = new HeaderReader();
                var imageFile = reader.Process(fileInfo);
                imageFile.HeaderHash = headerHash;
                imageFile.LibraryId = libraryId;
                UpdateNonHeaderFields(fileInfo, imageFile);
                return Result.Ok(new EntityMessage(fileInfo, imageFile));
            }
            catch (Exception ex)
            {
                errorProgressBlock.Post([new(fileInfo, "Header Error")]);
                Log.Error(ex, "Error processing image header in {Filename}", fileInfo.FullName);
                return Result.Fail(new PipelineError(fileInfo, "Error processing image header"));
            }
        },
        new ExecutionDataflowBlockOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = token,
        });

        // ************************************************
        // STEP 5: Batch image files to add to the database
        // ************************************************
        var block5BatchImageFiles = new BatchBlock<Result<EntityMessage>>(100,
            new GroupingDataflowBlockOptions
            {
                CancellationToken = token,
            });

        // ***************************************
        // STEP 6: Add new image files to database
        // ***************************************
        var block6AddToDatabase = new ActionBlock<Result<EntityMessage>[]>(results =>
        {
            var fileInfos = results.Select(r => r.Value.FileInfo);
            var imageFiles = results.Select(r => r.Value.ImageFile);

            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                dbContext.ImageFiles.AddRange(imageFiles);
                dbContext.SaveChanges();
                addedProgressBlock.Post(fileInfos
                    .Select(fileInfo => new IngestStatus(fileInfo, "Added"))
                    .ToList());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding images to the database");
                errorProgressBlock.Post(fileInfos
                    .Select(fileInfo => new IngestStatus(fileInfo, "Error adding to database"))
                    .ToList());
            }
        },
        new ExecutionDataflowBlockOptions
        {
            BoundedCapacity = 1,
            CancellationToken = token,
        });

        // ********************
        // Wire up the pipeline
        // ********************
        var options = new DataflowLinkOptions { PropagateCompletion = true };

        block1ComputeHash.LinkTo(block2BatchHashes, options, result => result.IsSuccess);
        block1ComputeHash.LinkTo(DataflowBlock.NullTarget<Result<HashMessage>>());

        block2BatchHashes.LinkTo(block3GetOrCreateEntity, options);

        block3GetOrCreateEntity.LinkTo(block4ParseHeader, options);

        block4ParseHeader.LinkTo(block5BatchImageFiles, options, result => result.IsSuccess);
        block4ParseHeader.LinkTo(DataflowBlock.NullTarget<Result<EntityMessage>>());

        block5BatchImageFiles.LinkTo(block6AddToDatabase, options);

        Task completion = block6AddToDatabase.Completion
            .ContinueWith(_ =>
            {
                addedProgressBlock.Complete();
                updatedProgressBlock.Complete();
                skippedProgressBlock.Complete();
                errorProgressBlock.Complete();
            });

        return (block1ComputeHash, completion);
    }

    private static bool UpdateNonHeaderFields(IFileInfo fileInfo, ImageFile imageFile)
    {
        bool updated = false;

        if (imageFile.FileSize != fileInfo.Length)
        {
            imageFile.FileSize = fileInfo.Length;
            updated = true;
        }

        return updated;
    }

    private record FileMessage(IFileInfo FileInfo, int LibraryId);
    private record HashMessage(IFileInfo FileInfo, int LibraryId, string HeaderHash);
    private record EntityMessage(IFileInfo FileInfo, ImageFile ImageFile);

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
