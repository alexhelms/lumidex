﻿using Serilog;
using System.Buffers;
using System.Globalization;
using System.Xml.Linq;

namespace Lumidex.Core.IO;

public class XisfFile
{
    private readonly string _filename;

    private static readonly HashSet<string> _supportedXisfPropertyTypes =
    [
        "Boolean",
        "Int8",
        "UInt8",
        "Byte",
        "Int16",
        "Short",
        "UInt16",
        "UShort",
        "Int32",
        "Int",
        "UInt32",
        "UInt",
        "Float32",
        "Float64",
        "String",
        "TimePoint",
    ];

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
        const int XisfPreambleSize = 16;
        
        var fileInfo = new FileInfo(_filename);
        if (fileInfo.Exists == false)
        {
            throw new FileNotFoundException("XISF file not found", fileInfo.FullName);
        }

        if (fileInfo.Length < XisfPreambleSize)
        {
            throw new InvalidOperationException("File is not an XISF");
        }

        Span<byte> headerBuffer = stackalloc byte[XisfPreambleSize];
        using var fs = new FileStream(_filename, FileMode.Open, FileAccess.Read);
        fs.ReadExactly(headerBuffer);

        if (!XisfSignature.AsSpan().SequenceEqual(headerBuffer[..8]))
        {
            throw new InvalidOperationException("File is not an XISF");
        }

        int headerLength = Math.Max(0, BitConverter.ToInt32(headerBuffer[8..12]));
        headerLength = Math.Min(headerLength, (int)fileInfo.Length);

        var header = new ImageHeader();
        header.FileExtension = ".xisf";

        // Read the xml header
        var buffer = ArrayPool<byte>.Shared.Rent(headerLength);
        try
        {
            fs.ReadExactly(buffer, 0, headerLength);
            MemoryStream ms = new(buffer, 0, headerLength);
            XDocument doc = XDocument.Load(ms);
            XNamespace ns = "http://www.pixinsight.com/xisf";
            XElement xisfNode = doc.Nodes().OfType<XElement>().First(x => x.Name == ns + "xisf");
            if (xisfNode.Elements(ns + "Image").FirstOrDefault() is { } imageNode)
            {
                var geometry = imageNode.Attribute("geometry")?.Value.Split(':');
                if (geometry is { Length: 3 })
                {
                    header.Width = int.Parse(geometry[0]);
                    header.Height= int.Parse(geometry[1]);
                }

                var fitsKeywords = imageNode.Elements(ns + "FITSKeyword");
                ParseFitsKeywords(fileInfo, header, fitsKeywords);

                var xisfProperties = imageNode.Elements(ns + "Property");
                ParseXisfProperties(fileInfo, header, xisfProperties);
            }
            else
            {
                throw new Exception($"Image header not found in XISF {_filename}");
            }
        }
        catch(Exception e)
        {
            Log.Error(e, "Error reading XISF header {Filename}", fileInfo.FullName);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        return header;
    }

    private static void ParseFitsKeywords(FileInfo fileInfo, ImageHeader header, IEnumerable<XElement> elements)
    {
        foreach (var item in elements)
        {
            var keyword = item.Attribute("name")!.Value;
            var comment = item.Attribute("comment")?.Value ?? string.Empty;
            var rawValue = item.Attribute("value")!.Value;
            var rawValueLower = rawValue.ToLowerInvariant();

            try
            {
                if (keyword == "COMMENT" || keyword == "HISTORY")
                {
                    header.Items.Add(new StringHeaderEntry(keyword, string.Empty, comment));
                }
                // Boolean
                else if (rawValueLower.Length == 1 && (rawValueLower == "t" || rawValueLower == "f"))
                {
                    header.Items.Add(new BooleanHeaderEntry(keyword, rawValueLower == "t", comment));
                }
                // Floating point
                else if (rawValue.Contains('.') && double.TryParse(rawValue, CultureInfo.InvariantCulture, out var d))
                {
                    header.Items.Add(new FloatHeaderEntry(keyword, d, comment));
                }
                // Integer
                else if (int.TryParse(rawValue, CultureInfo.InvariantCulture, out var i))
                {
                    header.Items.Add(new IntegerHeaderEntry(keyword, i, comment));
                }
                // Complex
                else if (rawValue.StartsWith('(') && rawValue.EndsWith(')') && rawValue.Contains(','))
                {
                    Log.Debug("XISF header {Keyword} = `{Value}` complex type ignored in {Filename}", keyword, rawValue, fileInfo.FullName);
                }
                // String
                // NINA saving raws as XISF does not wrap strings in single quotes like FITS.
                else
                {
                    // Probably XISF converted from FITS
                    if (rawValue.StartsWith('\''))
                    {
                        // Single quotes within the string are escaped with preceding single quote.
                        rawValue = rawValue.Replace("\'\'", "\'");

                        // Strip the single quote at the beginning and end.
                        header.Items.Add(new StringHeaderEntry(keyword, rawValue[1..^1], comment));
                    }
                    // Probably XISF created by NINA
                    else
                    {
                        header.Items.Add(new StringHeaderEntry(keyword, rawValue, comment));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unhandled error parsing XISF FITS header {Keyword} = `{Value}` in {Filename}", keyword, rawValue, fileInfo.FullName);
            }
        }
    }

    private static void ParseXisfProperties(FileInfo fileInfo, ImageHeader header, IEnumerable<XElement> elements)
    {
        // Very minimal support of the entire xisf spec. Only supporting bare minimum to determine ImageKind.

        foreach (var item in elements)
        {
            string id = item.Attribute("id")!.Value;
            string type = item.Attribute("type")!.Value;
            string? value = type == "String"
                ? item.Value    // String type always puts the value inside the element instead of the `value` attribute
                : item.Attribute("value")?.Value;

            if (!_supportedXisfPropertyTypes.Contains(type))
            {
                Log.Debug("XISF property header {Id} is an unsupported type {Type}", id, type);
                continue;
            }

            if (value is null)
            {
                Log.Debug("XISF property header {Id} value is in an unsupported format", id, type);
                continue;
            }

            try
            {
                switch (type)
                {
                    case "Boolean":
                        header.Items.Add(new BooleanHeaderEntry(id, value == "1"));
                        break;

                    case "Int8":
                    case "UInt8":
                    case "Byte":
                    case "Int16":
                    case "Short":
                    case "UInt16":
                    case "UShort":
                    case "Int32":
                    case "Int":
                    case "UInt32":
                    case "UInt":
                        header.Items.Add(new IntegerHeaderEntry(id, int.Parse(value, CultureInfo.InvariantCulture)));
                        break;

                    case "Float32":
                    case "Float64":
                        header.Items.Add(new FloatHeaderEntry(id, double.Parse(value, CultureInfo.InvariantCulture)));
                        break;

                    case "String":
                    case "TimePoint":
                        header.Items.Add(new StringHeaderEntry(id, value));
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unhandled error parsing XISF property header {Id} of type {Type} in {Filename}", id, type, fileInfo.FullName);
            }
        }
    }
}
