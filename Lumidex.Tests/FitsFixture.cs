using System.IO.Abstractions;
using System.Text;
using Lumidex.Core.IO;

namespace Lumidex.Tests;

/// <summary>
/// Builds minimal, structurally valid binary FITS files so tests can exercise the native
/// cfitsio-backed <see cref="FitsFile.ReadHeader"/> path with arbitrary header cards, mirroring
/// how <see cref="XisfFixture"/> builds XISF files for the XISF FITSKeyword parsing path.
/// </summary>
public class FitsFixture : IDisposable
{
    private const int CardWidth = 80;
    private const int BlockSize = 2880;

    private readonly TemporaryDirectory _tempDir = new();

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    public IFileInfo GenerateFitsFile(params FitsHeaderContent[] headerItems)
    {
        var cards = new List<string>
        {
            RawValueCard("SIMPLE", "T", "conforms to FITS standard"),
            RawValueCard("BITPIX", "8", "8-bit unsigned integers"),
            RawValueCard("NAXIS", "0", "no data array"),
            RawValueCard("EXTEND", "T", "may contain extensions"),
        };

        foreach (var item in headerItems)
        {
            cards.Add(RawValueCard(item.Keyword, item.Value, item.Comment));
        }

        cards.Add("END".PadRight(CardWidth));

        var headerBytes = Encoding.ASCII.GetBytes(string.Concat(cards));
        var remainder = headerBytes.Length % BlockSize;
        if (remainder != 0)
        {
            var padded = new byte[headerBytes.Length + (BlockSize - remainder)];
            Array.Copy(headerBytes, padded, headerBytes.Length);
            Array.Fill(padded, (byte)' ', headerBytes.Length, padded.Length - headerBytes.Length);
            headerBytes = padded;
        }

        var filename = Path.Join(_tempDir.Path, Path.GetRandomFileName() + ".fits");
        File.WriteAllBytes(filename, headerBytes);

        var fileSystem = new FileSystem();
        return fileSystem.FileInfo.Wrap(new FileInfo(filename));
    }

    // FITS header cards are fixed 80-character ASCII records: an 8-character keyword, "= ", a
    // right-justified value field, and an optional " / comment" trailer. Values are written raw
    // (unquoted) here since every case this fixture exists for is numeric.
    private static string RawValueCard(string keyword, string rawValue, string? comment)
    {
        var key = keyword.PadRight(8)[..8];
        var line = $"{key}= {rawValue.PadLeft(20)}";
        if (!string.IsNullOrEmpty(comment))
        {
            line += $" / {comment}";
        }

        return line.Length >= CardWidth ? line[..CardWidth] : line.PadRight(CardWidth);
    }
}

public record FitsHeaderContent(string Keyword, string Value, string? Comment = null);
