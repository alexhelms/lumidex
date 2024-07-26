namespace Lumidex.Core.IO;

public sealed class TemporaryDirectory : IDisposable
{
    private bool _disposed;

    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(Path);
    }

    public TemporaryDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Path = path;
    }

    public string Path { get; private set; }

    ~TemporaryDirectory()
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
                Directory.Delete(Path, true);
            }
            catch
            {
                // best effort
            }

            _disposed = true;
        }
    }
}