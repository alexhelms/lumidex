using Lumidex.Core.Detection;
using TUnit.Core.Executors;

namespace Lumidex.Tests;

public class HeaderReaderCultureTests : XisfFixture
{
    [Test]
    [Culture("de-DE")]
    public async Task IntegerValue_Euro()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Exposure).IsEqualTo(4);
        await Assert.That(imageFile.Exposure.ToString()).IsEqualTo("4");
    }

    [Test]
    [Culture("de-DE")]
    public async Task DoubleValue_Euro()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4.2"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Exposure).IsEqualTo(4.2);
        await Assert.That(imageFile.Exposure.ToString()).IsEqualTo("4,2");
    }

    [Test]
    [Culture("en-US")]
    public async Task IntegerValue_USA()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Exposure).IsEqualTo(4);
        await Assert.That(imageFile.Exposure.ToString()).IsEqualTo("4");
    }

    [Test]
    [Culture("en-US")]
    public async Task DoubleValue_USA()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4.2"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Exposure).IsEqualTo(4.2);
        await Assert.That(imageFile.Exposure.ToString()).IsEqualTo("4.2");
    }
}