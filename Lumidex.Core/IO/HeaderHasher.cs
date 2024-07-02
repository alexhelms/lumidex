namespace Lumidex.Core.IO;

public abstract class HeaderHasher
{
    public abstract Task<byte[]> ComputeHashAsync(string filename, CancellationToken token = default);
}
