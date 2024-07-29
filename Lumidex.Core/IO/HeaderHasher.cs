namespace Lumidex.Core.IO;

public abstract class HeaderHasher
{
    public static HeaderHasher FromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".xisf" => new XisfHeaderHasher(),
        ".fits" => new FitsHeaderHasher(),
        ".fit" => new FitsHeaderHasher(),
        _ => throw new Exception($"Unsupported file extension: {extension}"),
    };

    public abstract Task<byte[]> ComputeHashAsync(string filename, CancellationToken token = default);
}
