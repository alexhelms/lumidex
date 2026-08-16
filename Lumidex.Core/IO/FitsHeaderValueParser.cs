using System.Globalization;

namespace Lumidex.Core.IO;

/// <summary>
/// Parses raw FITS header value strings — as found both in native FITS keyword records and in XISF
/// FITSKeyword elements (which carry the original unquoted FITS value text) — into numeric types,
/// following the same permissive grammar cfitsio itself uses when classifying header values.
/// </summary>
public static class FitsHeaderValueParser
{
    public static bool TryParseInteger(string rawValue, out int value)
        => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Parses a FITS real value. Accepts the standard 'E'/'e' exponent marker as well as the
    /// Fortran-style 'D'/'d' marker some FITS-writing software uses to flag double precision
    /// (e.g. "2.956025D+02"), and values with an exponent but no decimal point (e.g. "296E+02").
    /// cfitsio accepts all of these natively when reading a native FITS file; .NET's double parser
    /// only recognizes 'E'/'e', so 'D'/'d' is normalized to 'E' before parsing.
    /// </summary>
    public static bool TryParseFloat(string rawValue, out double value)
    {
        var normalized = rawValue.IndexOfAny(['D', 'd']) >= 0
            ? rawValue.Replace('D', 'E').Replace('d', 'e')
            : rawValue;

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
