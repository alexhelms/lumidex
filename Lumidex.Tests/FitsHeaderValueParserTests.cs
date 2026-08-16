using Lumidex.Core.IO;

namespace Lumidex.Tests;

public class FitsHeaderValueParserTests
{
    [Test]
    [Arguments("42", 42)]
    [Arguments("-42", -42)]
    [Arguments("+42", 42)]
    [Arguments("0", 0)]
    public async Task TryParseInteger_ParsesValidIntegers(string raw, int expected)
    {
        var success = FitsHeaderValueParser.TryParseInteger(raw, out var value);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    // Values with a decimal point or an exponent are real numbers per the FITS grammar, not
    // integers, even though some of them (e.g. "296E+02") have no fractional digits.
    [Test]
    [Arguments("4.2")]
    [Arguments("2.956025E+02")]
    [Arguments("296E+02")]
    [Arguments("2.956025D+02")]
    public async Task TryParseInteger_RejectsRealNumbers(string raw)
    {
        var success = FitsHeaderValueParser.TryParseInteger(raw, out _);

        await Assert.That(success).IsFalse();
    }

    [Test]
    [Arguments("2.956025E+02", 295.6025)]
    [Arguments("2.956025e+02", 295.6025)]
    [Arguments("-2.956025E+02", -295.6025)]
    [Arguments("+2.956025E+02", 295.6025)]
    [Arguments("2.956025E-02", 0.02956025)]
    // Fortran-style double-precision exponent marker, as emitted by some FITS-writing software.
    [Arguments("2.956025D+02", 295.6025)]
    [Arguments("2.956025d+02", 295.6025)]
    [Arguments("-2.956025D-02", -0.02956025)]
    // Exponential notation with no decimal point in the mantissa.
    [Arguments("296E+02", 29600d)]
    [Arguments("1E-05", 0.00001)]
    // Plain fixed-point values should still parse.
    [Arguments("4.2", 4.2)]
    [Arguments("-4.2", -4.2)]
    public async Task TryParseFloat_ParsesRealNumbers(string raw, double expected)
    {
        var success = FitsHeaderValueParser.TryParseFloat(raw, out var value);

        await Assert.That(success).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("NOTANUMBER")]
    [Arguments("REDCAM")]
    [Arguments("'42'")]
    [Arguments("(1,2)")]
    [Arguments("T")]
    [Arguments("")]
    public async Task TryParseFloat_RejectsNonNumericValues(string raw)
    {
        var success = FitsHeaderValueParser.TryParseFloat(raw, out _);

        await Assert.That(success).IsFalse();
    }
}
