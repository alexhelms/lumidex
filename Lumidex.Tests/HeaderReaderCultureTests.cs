using Lumidex.Core.Detection;

namespace Lumidex.Tests;

public class HeaderReaderCultureTests : XisfFixture
{
    [Fact]
    [UseCulture("de-DE")]
    public void IntegerValue_Euro()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        imageFile.Exposure.Should().Be(4);
        imageFile.Exposure.ToString().Should().Be("4");
    }

    [Fact]
    [UseCulture("de-DE")]
    public void DoubleValue_Euro()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4.2"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        imageFile.Exposure.Should().Be(4.2);
        imageFile.Exposure.ToString().Should().Be("4,2");
    }

    [Fact]
    [UseCulture("en-US")]
    public void IntegerValue_USA()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        imageFile.Exposure.Should().Be(4);
        imageFile.Exposure.ToString().Should().Be("4");
    }

    [Fact]
    [UseCulture("en-US")]
    public void DoubleValue_USA()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("EXPOSURE", "4.2"));
        var reader = new HeaderReader();
        var imageFile = reader.Process(fileInfo);

        imageFile.Exposure.Should().Be(4.2);
        imageFile.Exposure.ToString().Should().Be("4.2");
    }
}