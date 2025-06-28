using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Serilog;
using System.IO.Abstractions;
using System.Linq.Expressions;
using System.Reflection;

namespace Lumidex.Core.Detection;

public class HeaderReader
{
    public ImageFile Process(IFileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Image file not found", fileInfo.FullName);
        }

        var header = GetHeader(fileInfo);
        var imageFile = Process(fileInfo.Name, header);
        imageFile.Path = fileInfo.FullName;

        return imageFile;
    }

    public ImageFile Process(string filename, ImageHeader header)
    {
        ImageFile imageFile = new();

        imageFile.Type = DetermineImageType(header);
        imageFile.Kind = DetermineImageKind(header, imageFile.Type, filename);

        ExtractCameraKeywords(header, imageFile);
        ExtractFocuserKeywords(header, imageFile);
        ExtractRotatorKeywords(header, imageFile);
        ExtractFilterWheelKeywords(header, imageFile);
        ExtractMountKeywords(header, imageFile);
        ExtractTelescopeKeywords(header, imageFile);
        ExtractTargetKeywords(header, imageFile);
        ExtractSiteKeywords(header, imageFile);
        ExtractWeatherKeywords(header, imageFile);

        return imageFile;
    }

    private ImageHeader GetHeader(IFileInfo fileInfo)
    {
        var extension = fileInfo.Extension.ToLowerInvariant();
        if (extension == ".xisf")
        {
            var xisf = new XisfFile(fileInfo.FullName);
            return xisf.ReadHeader();
        }
        else if (extension.StartsWith(".fit"))
        {
            using var fits = new FitsFile(fileInfo.FullName, FitsFile.IoMode.Read);
            return fits.ReadHeader();
        }
        else
        {
            throw new NotImplementedException($"{extension} not supported");
        }
    }

    private ImageType DetermineImageType(ImageHeader header)
    {
        HeaderEntry<string>? imageTypeEntry = header.GetEntry<string>("IMAGETYP");
        imageTypeEntry ??= header.GetEntry<string>("FRAMETYP");

        if (imageTypeEntry is { Value: { } })
        {
            switch (imageTypeEntry.Value.ToLowerInvariant().Replace(" ", string.Empty))
            {
                case "light":
                case "lightframe":
                case "science":
                case "scienceframe":
                case "masterlight":
                    return ImageType.Light;
                case "bias":
                case "biasframe":
                case "masterbias":
                    return ImageType.Bias;
                case "dark":
                case "darkframe":
                case "masterdark":
                case "flatdark":
                case "darkflat":
                    return ImageType.Dark;
                case "flat":
                case "flatframe":
                case "flatfield":
                case "masterflat":
                    return ImageType.Flat;
            }
        }

        return ImageType.Unknown;
    }

    private ImageKind DetermineImageKind(ImageHeader header, ImageType imageType, string filename)
    {
        // TODO: Siril support.

        string extension = Path.GetExtension(filename);

        if (header.Items.Count == 0) return ImageKind.Processed;

        if (header.GetEntry<string>("IMAGETYP") is { Value: { } } imageTypeEntry)
        {
            if (imageTypeEntry.Value.AsSpan().Contains("master", StringComparison.OrdinalIgnoreCase))
                return ImageKind.Master;
        }

        var historyAndCommentItems = header.Items
                .Where(item => item.Keyword == "HISTORY" || item.Keyword == "COMMENT")
                .ToList();

        bool isIntermediate = historyAndCommentItems
            .Any(item =>
            {
                return item.Comment.AsSpan().StartsWith("Calibration with PixInsight")
                     || item.Comment.AsSpan().StartsWith("CosmeticCorrection with PixInsight")
                     || item.Comment.AsSpan().StartsWith("Registration with PixInsight")
                     || item.Comment.AsSpan().StartsWith("Debayer with PixInsight")
                     || item.Comment.AsSpan().StartsWith("LocalNormalization with PixInsight");
            });

        var hasBeenIntegrated = historyAndCommentItems
                .Any(item => item.Comment.AsSpan().StartsWith("Integration with PixInsight"));

        // Pix intermediate files are only xisf
        if (extension == ".xisf")
        {
            if (isIntermediate) return ImageKind.Intermediate;

            // PixInsight 1.9.x no longer adds FITS HISTORY headers.
            // Instead it uses the XISFProperty element that are distinct from the FITSKeyword element in the XISF spec.
            // Look at each XISFProperty for the processes that make this header an "intermediate" image kind.
            
            var xisfPropertyIds = header.Items
                .Where(item => item.Keyword.AsSpan().StartsWith("PCL:Signature"))
                .Select(item => item.Keyword)
                .ToHashSet();

            if (xisfPropertyIds.Contains("PCL:Signature:Calibration")
                || xisfPropertyIds.Contains("PCL:Signature:CosmeticCorrection")
                || xisfPropertyIds.Contains("PCL:Signature:Registration")
                || xisfPropertyIds.Contains("PCL:Signature:Debayer")
                || xisfPropertyIds.Contains("PCL:Signature:LocalNormalization"))
                return ImageKind.Intermediate;
        }

        if (header.GetEntry<string>("PROGRAM") is { Value: { } } programEntry)
        {
            //Siril always adds the PROGRAM header with the value "Siril <version number>"
            if (programEntry.Value.AsSpan().Contains("Siril", StringComparison.OrdinalIgnoreCase))
            {

                if (header.GetEntry<int>("STACKCNT") is { Value: { } } stackCountEntry)
                {
                    //Siril always adds this header for stacked images,
                    //So must be either a Master or Intermediate image.
                    //Siril does not add headers that identify whether the image is Flat, Dark, or Bias
                    //This info must come from capture software.

                    switch (imageType)
                    {
                        case ImageType.Flat:
                        case ImageType.Dark:
                        case ImageType.Bias:
                            return ImageKind.Master;
                        case ImageType.Light:
                        case ImageType.Unknown:
                            return ImageKind.Intermediate;
                    }
                }

                //Not stacked so either a Calibration or Intermediate image.
                switch (imageType)
                {
                    case ImageType.Flat:
                    case ImageType.Dark:
                    case ImageType.Bias:
                        return ImageKind.Calibration;
                    case ImageType.Light:
                    case ImageType.Unknown:
                        return ImageKind.Intermediate;
                }

            }
        }

        if (isIntermediate == false &&
            hasBeenIntegrated == false)
        {
            switch (imageType)
            {
                case ImageType.Flat:
                case ImageType.Dark:
                case ImageType.Bias:
                    return ImageKind.Calibration;
                case ImageType.Light:
                    return ImageKind.Raw;
            }
        }

        return ImageKind.Unknown;
    }

    private void ExtractKeyword<TKeyword, TImageFile>(
        ImageHeader header,
        ImageFile imageFile,
        Expression<Func<ImageFile, TImageFile?>> propertySelector,
        params string[] keywords)
        where TKeyword : IComparable
    {
        PropertyInfo propInfo = propertySelector.GetPropertyInfo();

        foreach (string keyword in keywords)
        {
            if (header.GetEntry(keyword) is HeaderEntry<TKeyword> and { Value: not null } instrumentEntry)
            {
                try
                {
                    if (instrumentEntry.Value is string s)
                    {
                        propInfo.SetValue(imageFile, s.Trim());
                    }
                    else
                    {
                        propInfo.SetValue(imageFile, instrumentEntry.Value);
                    }
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error extracting {Keyword} from {Entry}", keyword, instrumentEntry);
                    throw;
                }
            }
        }
    }

    private void ExtractKeyword<TKeyword, TImageFile>(
        ImageHeader header,
        ImageFile imageFile,
        Expression<Func<ImageFile, TImageFile?>> propertySelector,
        Func<TKeyword, TImageFile?> transformFunc,
        params string[] keywords)
        where TKeyword : IComparable
    {
        PropertyInfo propInfo = propertySelector.GetPropertyInfo();
        string currentKeyword = string.Empty;
        string? currentValueStr = string.Empty;

        try
        {
            foreach (string keyword in keywords)
            {
                currentKeyword = keyword;

                if (header.GetEntry(keyword) is HeaderEntry<TKeyword> and { Value: { } } instrumentEntry)
                {
                    try
                    {

                        currentValueStr = instrumentEntry.Value.ToString();
                        propInfo.SetValue(imageFile, transformFunc(instrumentEntry.Value));
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error extracting {Keyword} from {Entry}", keyword, instrumentEntry);
                        throw;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error transforming header keyword `{Keyword}` = `{Value}`", currentKeyword, currentValueStr);
            throw;
        }
    }

    private void ExtractCameraKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, string?>(header, imageFile, x => x.CameraName!,
            "INSTRUME");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Exposure, 
            "EXPOSURE", "EXPTIME");
        ExtractKeyword<double, double?>(header, imageFile, x => x.CameraTemperatureSetPoint,
            "SET-TEMP", "CCD-TSET");
        ExtractKeyword<int, double?>(header, imageFile, x => x.CameraTemperatureSetPoint,
            x => (double)x,
            "SET-TEMP", "CCD-TSET");
        ExtractKeyword<double, double?>(header, imageFile, x => x.CameraTemperature,
            "CCD-TEMP");
        ExtractKeyword<int, double?>(header, imageFile, x => x.CameraTemperature,
            x => (double)x,
            "CCD-TEMP");
        ExtractKeyword<int, int?>(header, imageFile, x => x.CameraGain,
            "GAIN", "CCD-GAIN", "EGAIN");
        ExtractKeyword<int, int?>(header, imageFile, x => x.CameraOffset,
            "OFFSET");
        ExtractKeyword<int, int?>(header, imageFile, x => x.Binning,
            "XBINNING", "CCDXBIN");
        ExtractKeyword<double, int?>(header, imageFile, x => x.Binning,
            x => (int)x, // Some software writes these as doubles instead of integers
            "XBINNING", "CCDXBIN");
        ExtractKeyword<double, double?>(header, imageFile, x => x.PixelSize,
            "XPIXSZ");
        ExtractKeyword<string, string?>(header, imageFile, x => x.ReadoutMode!,
            "READOUTM");

        // SHARPCAP may save integer exposures as an integer instead of a float because it is missing the decimal
        if (imageFile.Exposure.HasValue == false)
        {
            ExtractKeyword<int, double?>(header, imageFile, x => x.Exposure,
                x => (double)x,
                "EXPOSURE", "EXPTIME");
        }
    }

    private void ExtractFocuserKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, string?>(header, imageFile, x => x.FocuserName!,
            "FOCNAME", "FOCUSER");
        ExtractKeyword<int, int?>(header, imageFile, x => x.FocuserPosition,
            "FOCPOS", "FOCUSPOS");
        ExtractKeyword<double, double?>(header, imageFile, x => x.FocuserTemperature,
            "FOCTEMP");
    }

    private void ExtractRotatorKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, string?>(header, imageFile, x => x.RotatorName!,
            "ROTNAME");
        ExtractKeyword<double, double?>(header, imageFile, x => x.RotatorPosition,
            "ROTATOR", "ROTATANG");
    }

    private void ExtractFilterWheelKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, string?>(header, imageFile, x => x.FilterWheelName!,
            "FWHEEL");
        ExtractKeyword<string, string?>(header, imageFile, x => x.FilterName!,
            "FILTER");
    }

    private void ExtractMountKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<double, double?>(header, imageFile, x => x.RightAscension,
            "RA");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Declination,
            "DEC");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Altitude,
            "CENTALT", "OBJCTALT");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Azimuth,
            "CENTAZ", "OBJCTAZ");
    }

    private void ExtractTelescopeKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, string?>(header, imageFile, x => x.TelescopeName!,
            "TELESCOP");
        ExtractKeyword<double, double?>(header, imageFile, x => x.FocalLength,
            "FOCALLEN");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Airmass,
            "AIRMASS");
    }

    private void ExtractTargetKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<string, DateTime?>(header, imageFile, x => x.ObservationTimestampUtc,
            static rawValue =>
            {
                if (DateTime.TryParse(rawValue, out var timestamp))
                {
                    var utc = timestamp.ToUniversalTime();
                    return utc;
                }
                return timestamp;
            },
            "DATE-OBS");
        ExtractKeyword<string, DateTime?>(header, imageFile, x => x.ObservationTimestampLocal,
            static rawValue =>
            {
                if (DateTime.TryParse(rawValue, out var timestamp))
                {
                    return timestamp;
                }
                return DateTime.MinValue;
            },
            "DATE-LOC");
        ExtractKeyword<string, string?>(header, imageFile, x => x.ObjectName!,
            "OBJECT");
    }

    private void ExtractSiteKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<double, double?>(header, imageFile, x => x.Latitude,
            "SITELAT", "OBSLAT");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Longitude,
            "SITELONG", "SITELON", "OBSLONG");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Elevation,
            "SITEELEV", "SITEELV");
    }

    private void ExtractWeatherKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<double, double?>(header, imageFile, x => x.DewPoint,
            "DEWPOINT");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Humidity,
            "HUMIDITY");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Pressure,
            "PRESSURE");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Temperature,
            "AMBTEMP");
    }
}
