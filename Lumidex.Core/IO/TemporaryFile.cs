namespace Lumidex.Core.IO;

public sealed class TemporaryFile : IDisposable
{
    private bool _disposed;

    public TemporaryFile()
        : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName()))
    {
    }

    public TemporaryFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    ~TemporaryFile()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            try
            {
                if (File.Exists(Path))
                    File.Delete(Path);
            }
            catch
            {
                // best effort
            }

            _disposed = true;
        }
    }
}
