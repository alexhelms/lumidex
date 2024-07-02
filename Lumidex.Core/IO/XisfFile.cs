using Serilog;
using System.Buffers;
using System.Xml.Linq;

namespace Lumidex.Core.IO;

public class XisfFile
{
    private readonly string _filename;

    internal static byte[] XisfSignature = [
        // XISF0100
        0x58, 0x49, 0x53, 0x46, 0x30, 0x31, 0x30, 0x30
    ];

    public XisfFile(string filename)
    {
        _filename = filename;
    }

    public ImageHeader ReadHeader()
    {
        Span<byte> headerBuffer = stackalloc byte[16];
        var fileInfo = new FileInfo(_filename);
        using var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read);
        fs.ReadExactly(headerBuffer);

        if (!XisfSignature.AsSpan().SequenceEqual(headerBuffer[..8]))
        {
            throw new InvalidOperationException("File is not an XISF");
        }

        int headerLength = Math.Max(0, BitConverter.ToInt32(headerBuffer[8..12]));
        headerLength = Math.Min(headerLength, (int)fileInfo.Length);

        var header = new ImageHeader();

        // Read the xml header
        var buffer = ArrayPool<byte>.Shared.Rent(headerLength);
        try
        {
            fs.ReadExactly(buffer);
            MemoryStream ms = new(buffer, 0, headerLength);
            XDocument doc = XDocument.Load(ms);
            XNamespace ns = "http://www.pixinsight.com/xisf";
            XElement xisfNode = doc.Nodes().OfType<XElement>().First(x => x.Name == ns + "xisf");
            if (xisfNode.Elements(ns + "Image").FirstOrDefault() is { } imageNode)
            {
                var keywords = imageNode.Elements(ns + "FITSKeyword");
                foreach (var item in keywords)
                {
                    var keyword = item.Attribute("name")!.Value;
                    var comment = item.Attribute("comment")?.Value ?? string.Empty;
                    var rawValue = item.Attribute("value")!.Value;
                    var rawValueLower = rawValue.ToLowerInvariant();

                    try
                    {
                        if (keyword == "NAXIS1")
                            header.Width = int.Parse(rawValue);
                        else if (keyword == "NAXIS2")
                            header.Height = int.Parse(rawValue);

                        if (keyword == "COMMENT" || keyword == "HISTORY")
                        {
                            header.Items.Add(new StringHeaderEntry(keyword, string.Empty, comment));
                        }
                        // String
                        else if (rawValue.StartsWith('\''))
                        {
                            // Single quotes within the string are escaped with preceding single quote.
                            rawValue = rawValue.Replace("\'\'", "\'");

                            // Strip the single quote at the beginning and end.
                            header.Items.Add(new StringHeaderEntry(keyword, rawValue[1..^1], comment));
                        }
                        // Boolean
                        else if (rawValueLower == "t" || rawValueLower == "f")
                        {
                            header.Items.Add(new BooleanHeaderEntry(keyword, rawValueLower == "t", comment));
                        }
                        // Floating point
                        else if (rawValue.Contains('.') && double.TryParse(rawValue, out var d))
                        {
                            header.Items.Add(new FloatHeaderEntry(keyword, d, comment));
                        }
                        // Integer
                        else if (int.TryParse(rawValue, out var i))
                        {
                            header.Items.Add(new IntegerHeaderEntry(keyword, i, comment));
                        }
                        // Complex
                        else if (rawValue.StartsWith('('))
                        {
                            Log.Warning("XISF header {Keyword} = `{Value}` complex type ignored", keyword, rawValue);
                        }
                        else
                        {
                            Log.Warning("XISF header {Keyword} = `{Value}` type inference failed", keyword, rawValue);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Unhandled error parsing XISF header {Keyword} = `{Value}`", keyword, rawValue);
                    }
                }
            }
            else
            {
                throw new Exception("Image header not found in XISF");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        return header;
    }
}
