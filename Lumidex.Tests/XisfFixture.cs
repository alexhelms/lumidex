using Lumidex.Core.IO;
using System.IO.Abstractions;
using System.Text;

namespace Lumidex.Tests;

public class XisfFixture : IDisposable
{
    private readonly TemporaryDirectory _tempDir = new();

    public void Dispose()
    {
        _tempDir.Dispose();
    }

    public IFileInfo GenerateXisfFile(params XisfHeaderContent[] headerItems)
    {
        var xisfXmlBeginning =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <!--
            Extensible Image Serialization Format - XISF version 1.0
            Created with PixInsight software - http://pixinsight.com/
            -->
            <xisf version="1.0"
            	xmlns="http://www.pixinsight.com/xisf"
            	xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://www.pixinsight.com/xisf http://pixinsight.com/xisf/xisf-1.0.xsd">
            	<Image geometry="9576:6388:1" sampleFormat="Float32" bounds="0:1" colorSpace="Gray" location="attachment:12288:104433408">
                    <FITSKeyword name="SIMPLE" value="T" comment="file does conform to FITS standard"/>
                    <FITSKeyword name="BITPIX" value="16" comment="number of bits per data pixel"/>
                    <FITSKeyword name="NAXIS" value="2" comment="number of data axes"/>
                    <FITSKeyword name="NAXIS1" value="6252" comment="length of data axis 1"/>
                    <FITSKeyword name="NAXIS2" value="4176" comment="length of data axis 2"/>
                    <FITSKeyword name="EXTEND" value="T" comment="FITS dataset may contain extensions"/>
            """;

        var xisfXmlEnd =
            """
                </Image>
            </xisf>
            """;

        var sb = new StringBuilder();
        sb.Append(xisfXmlBeginning);
        foreach (var item in headerItems)
        {
            if (item.Comment is null)
            {
                sb.Append($"""<FITSKeyword name="{item.Keyword}" value="{item.Value}" />""");
            }
            else
            {
                sb.Append($"""<FITSKeyword name="{item.Keyword}" value="{item.Value}" comment="{item.Comment ?? string.Empty}"/>""");
            }
        }
        sb.Append(xisfXmlEnd);

        Span<byte> xmlBytes = Encoding.UTF8.GetBytes(sb.ToString());
        Span<byte> xmlSizeBytes = BitConverter.GetBytes((uint)xmlBytes.Length);
        Span<byte> xisfMagicBytes = Encoding.UTF8.GetBytes("XISF0100");
        Span<byte> xisfReserved = stackalloc byte[4];

        var xmlStream = new MemoryStream(2 * xmlBytes.Length);
        xmlStream.Write(xisfMagicBytes);
        xmlStream.Write(xmlSizeBytes);
        xmlStream.Write(xisfReserved);
        xmlStream.Write(xmlBytes);
        xmlStream.Flush();
        xmlStream.Position = 0;

        var filename = Path.Join(_tempDir.Path, Path.GetRandomFileName() + ".xisf");
        using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
        {
            xmlStream.CopyTo(fs);
        }

        var fileSystem = new FileSystem();
        var fileInfo = fileSystem.FileInfo.Wrap(new FileInfo(filename));
        return fileInfo;
    }
}

public record XisfHeaderContent(string Keyword, string Value, string? Comment = null);