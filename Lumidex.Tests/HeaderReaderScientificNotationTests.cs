using Lumidex.Core.Detection;
using Lumidex.Core.IO;
using TUnit.Core.Executors;

namespace Lumidex.Tests;

// Regression coverage for scientific-notation FITS header values (e.g. "2.956025E+02") that were
// silently mistyped as strings instead of numbers. AIRMASS is read as a double via
// HeaderReader.ExtractTelescopeKeywords, so it's a convenient real keyword to assert through.
public class HeaderReaderScientificNotationXisfTests : XisfFixture
{
    [Test]
    [Arguments("2.956025E+02", 295.6025)]
    [Arguments("2.956025e+02", 295.6025)]
    [Arguments("-2.956025E+02", -295.6025)]
    [Arguments("2.956025E-02", 0.02956025)]
    // Fortran-style 'D' double-precision exponent — valid FITS, previously fell through to string.
    [Arguments("2.956025D+02", 295.6025)]
    // Exponential notation with no decimal point — previously fell through to string.
    [Arguments("296E+02", 29600d)]
    public async Task ScientificNotationValue_ParsesAsFloat(string rawValue, double expected)
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("AIRMASS", rawValue));
        var reader = new HeaderReader();

        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Airmass).IsEqualTo(expected);
    }

    [Test]
    [Culture("de-DE")]
    [Arguments("2.956025E+02", 295.6025)]
    [Arguments("2.956025D+02", 295.6025)]
    public async Task ScientificNotationValue_ParsesAsFloat_RegardlessOfCulture(string rawValue, double expected)
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("AIRMASS", rawValue));
        var reader = new HeaderReader();

        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Airmass).IsEqualTo(expected);
    }

    [Test]
    public async Task PlainIntegerValue_StillParsesAsInteger()
    {
        var fileInfo = GenerateXisfFile(new XisfHeaderContent("CAMGAIN", "120"));

        var header = new XisfFile(fileInfo.FullName).ReadHeader();

        await Assert.That(header.GetEntry<int>("CAMGAIN")?.Value).IsEqualTo(120);
        await Assert.That(header.GetEntry<double>("CAMGAIN") is null).IsEqualTo(true);
    }
}

// The native .fits path goes through cfitsio (FitsFile.ReadHeader), which already parses all of
// these correctly — these are regression/consistency tests, not bug fixes, so the same values
// behave identically whichever file format they arrive in.
public class HeaderReaderScientificNotationFitsTests : FitsFixture
{
    [Test]
    [Arguments("2.956025E+02", 295.6025)]
    [Arguments("2.956025e+02", 295.6025)]
    [Arguments("-2.956025E+02", -295.6025)]
    [Arguments("2.956025E-02", 0.02956025)]
    [Arguments("2.956025D+02", 295.6025)]
    [Arguments("296E+02", 29600d)]
    public async Task ScientificNotationValue_ParsesAsFloat(string rawValue, double expected)
    {
        var fileInfo = GenerateFitsFile(new FitsHeaderContent("AIRMASS", rawValue));
        var reader = new HeaderReader();

        var imageFile = reader.Process(fileInfo);

        await Assert.That(imageFile.Airmass).IsEqualTo(expected);
    }
}
