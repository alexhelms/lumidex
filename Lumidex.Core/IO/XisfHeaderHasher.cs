using System.Security.Cryptography;

namespace Lumidex.Core.IO;

public class XisfHeaderHasher : HeaderHasher
{
    public override async Task<byte[]> ComputeHashAsync(string filename, CancellationToken token = default)
    {
        var fileInfo = new FileInfo(filename);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("XISF file not found", filename);
        }

        Memory<byte> headerBuffer = new byte[16];

        await using var fs = new FileStream(filename,FileMode.Open, FileAccess.Read);
        await fs.ReadExactlyAsync(headerBuffer);

        if (!XisfFile.XisfSignature.AsSpan().SequenceEqual(headerBuffer[..8].Span))
        {
            throw new InvalidOperationException("File is not an XISF");
        }

        int headerLength = Math.Max(0, BitConverter.ToInt32(headerBuffer[8..12].Span));
        headerLength = Math.Min(headerLength, (int) fileInfo.Length);

        int count = 0;
        int offset = 0;
        byte[] fileBuffer = new byte[8192];
        using var sha1 = SHA1.Create();

        while ((count = await fs.ReadAsync(fileBuffer, token)) != 0)
        {
            sha1.TransformBlock(fileBuffer, 0, count, null, 0);

            if (offset + count >= headerLength)
            {
                sha1.TransformFinalBlock(fileBuffer, 0, headerLength - offset);
                break;
            }

            offset += count;
        }

        return sha1.Hash ?? Array.Empty<byte>();
    }
}
