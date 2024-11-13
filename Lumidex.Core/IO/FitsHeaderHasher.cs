using System.Security.Cryptography;

namespace Lumidex.Core.IO;

public class FitsHeaderHasher : HeaderHasher
{
    // "END" followed by spaces to pad the column to 80 characters.
    private static readonly byte[] EndRecord = [
        0x45, 0x4e, 0x44, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
    ];

    public override async Task<byte[]> ComputeHashAsync(string filename, CancellationToken token = default)
    {
        var fileInfo = new FileInfo(filename);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("FITS file not found", filename);
        }

        const int RecordSize = 80;
        const int HeaderBlockSize = 2880;
        int count = 0;

        try
        {
            using var sha1 = SHA1.Create();

            // The buffer is large enough to read most files full headers
            // and small enough to not go on the large object heap.
            byte[] fileBuffer = new byte[6 * HeaderBlockSize];
            
            await using var fs = new FileStream(filename,FileMode.Open, FileAccess.Read);
            while ((count = await fs.ReadAsync(fileBuffer, token)) != 0)
            {
                for (int i = 0; i < count / RecordSize; i++)
                {
                    int offset = i * RecordSize;
                    bool isEndRecord = fileBuffer.AsSpan(offset, RecordSize).SequenceEqual(EndRecord);
                    if (isEndRecord)
                    {
                        sha1.TransformFinalBlock(fileBuffer, 0, 0);
                        goto exit;  // The one acceptable use of goto :)
                    }
                    else
                    {
                        sha1.TransformBlock(fileBuffer, offset, RecordSize, null, 0);
                    }
                }
            }

        exit:
            return sha1.Hash ?? [];
        }
        catch (CryptographicUnexpectedOperationException)
        {
            return [];
        }
    }
}