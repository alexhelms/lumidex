using Serilog;
using System.Buffers;
using System.Diagnostics;

namespace Lumidex.Core.IO;

public class FileExporter
{
    public async Task<int> CopyAsync(
        IEnumerable<string> filenames,
        string outputDirectory,
        bool overwrite = false,
        IProgress<FileExporterProgress>? progress = null,
        CancellationToken token = default)
    {
        var dirInfo = new DirectoryInfo(outputDirectory);

        if (!dirInfo.Exists)
            throw new DirectoryNotFoundException($"Output directory not found: {outputDirectory}");

        var allFileInfos = filenames.Select(f => new FileInfo(f)).ToList();
        var fileInfos = allFileInfos.Where(f => f.Exists).ToList();

        foreach (var file in allFileInfos.Where(f => !f.Exists))
        {
            Log.Warning("Cannot export file (Not Found) {Filename}", file.FullName);
        }

        long currentBytes = 0;
        int currentFileCount = 0;
        long totalBytes = fileInfos.Sum(f => f.Length);
        int totalFilesCount = fileInfos.Count;

        // Initial progress report so the totals are available
        SendProgress();

        var progressInterval = TimeSpan.FromMilliseconds(1000);
        var progressTimestamp = Stopwatch.GetTimestamp();

        const int BufferSize = 2097152; // 2 MiB
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            foreach (var fileInfo in allFileInfos.Where(f => f.Exists))
            {
                if (token.IsCancellationRequested) return currentFileCount;

                try
                {
                    var outputFilename = Path.Combine(dirInfo.FullName, fileInfo.Name);
                    var outputFileInfo = new FileInfo(outputFilename);

                    // Do not overwrite existing files if overwrite is false
                    if (outputFileInfo.Exists && !overwrite)
                    {
                        totalBytes -= outputFileInfo.Length;
                        totalFilesCount--;
                        continue;
                    }

                    await using var src = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
                    await using var dst = new FileStream(outputFilename, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, useAsync: true);

                    int bytesRead;
                    while ((bytesRead = await src.ReadAsync(new Memory<byte>(buffer), token).ConfigureAwait(false)) != 0)
                    {
                        await dst.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), token).ConfigureAwait(false);

                        currentBytes += bytesRead;

                        // Send out progress at well defined intervals
                        if (Stopwatch.GetElapsedTime(progressTimestamp) > progressInterval)
                        {
                            SendProgress();
                            progressTimestamp = Stopwatch.GetTimestamp();
                        }
                    }

                    currentFileCount++;
                    SendProgress();
                }
                catch (OperationCanceledException)
                {
                    return currentFileCount;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error copying {InputFilename} to {OutputFilename}");
                    throw;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // One last progress report so 100% reports correctly
        SendProgress();

        return currentFileCount;

        void SendProgress()
        {
            progress?.Report(new FileExporterProgress
            {
                CurrentBytes = currentBytes,
                TotalBytes = totalBytes,
                CurrentFileCount = currentFileCount,
                TotalFilesCount = totalFilesCount,
            });
        }
    }
}

public class FileExporterProgress
{
    public long CurrentBytes { get; init; }
    public long TotalBytes { get; init; }
    public int CurrentFileCount { get; init; }
    public int TotalFilesCount { get; init; }
}