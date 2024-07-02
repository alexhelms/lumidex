using Lumidex.Core.Data;
using Lumidex.Core.IO;
using Serilog;
using System.IO.Abstractions;
using System.Linq.Expressions;

namespace Lumidex.Core.Detection;

public class HeaderReader
{
    public ImageFile Process(IFileInfo fileInfo, string headerHash)
    {
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Image file not found", fileInfo.FullName);
        }

        var header = GetHeader(fileInfo);
        
        var imageFile = new ImageFile
        {
            HeaderHash = headerHash,
            Path = fileInfo.FullName,
        };

        imageFile.Type = DetermineImageType(header);
        imageFile.Kind = DetermineImageKind(header, imageFile.Type, fileInfo.Extension);

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
        if (header.GetEntry<string>("IMAGETYP") is { Value: { } } imageTypeEntry)
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

    private ImageKind DetermineImageKind(ImageHeader header, ImageType imageType, string extension)
    {
        // TODO: Siril support.

        if (header.Items.Count == 0) return ImageKind.Processed;

        if (header.GetEntry<string>("IMAGETYP") is { Value: { } } imageTypeEntry)
        {
            var imageTypeLower= imageTypeEntry.Value.ToLowerInvariant();
            if (imageTypeLower.Contains("master")) return ImageKind.Master;
        }

        var historyAndCommentItems = header.Items
                .Where(item => item.Keyword == "HISTORY" || item.Keyword == "COMMENT")
                .ToList();

        var hasBeenCalibrated = historyAndCommentItems
                .Any(item => item.Comment.StartsWith("Calibration with PixInsight"));

        var hasBeenCosmetized = historyAndCommentItems
                .Any(item => item.Comment.StartsWith("CosmeticCorrection with PixInsight"));

        var hasBeenRegistered = historyAndCommentItems
                .Any(item => item.Comment.StartsWith("Registration with PixInsight"));

        var hasBeenIntegrated = historyAndCommentItems
                .Any(item => item.Comment.StartsWith("Integration with PixInsight"));

        // Pix intermediate files are only xisf
        if (extension == ".xisf")
        {
            if (hasBeenCalibrated) return ImageKind.Intermediate;
            if (hasBeenCosmetized) return ImageKind.Intermediate;
            if (hasBeenRegistered) return ImageKind.Intermediate;
        }

        if (hasBeenCalibrated == false &&
            hasBeenCosmetized == false &&
            hasBeenRegistered == false &&
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
        var propInfo = propertySelector.GetPropertyInfo();

        foreach (var keyword in keywords)
        {
            if (header.GetEntry(keyword) is HeaderEntry<TKeyword> and { Value: not null } instrumentEntry)
            {
                propInfo.SetValue(imageFile, instrumentEntry.Value);
                break;
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
        var propInfo = propertySelector.GetPropertyInfo();
        var currentKeyword = string.Empty;
        var currentValueStr = string.Empty;

        try
        {
            foreach (var keyword in keywords)
            {
                currentKeyword = keyword;
                if (header.GetEntry(keyword) is HeaderEntry<TKeyword> and { Value: { } } instrumentEntry)
                {
                    currentValueStr = instrumentEntry.Value.ToString();
                    propInfo.SetValue(imageFile, transformFunc(instrumentEntry.Value));
                    break;
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
        ExtractKeyword<double, double?>(header, imageFile, x => x.CameraTemperature,
            "CCD-TEMP");
        ExtractKeyword<int, int?>(header, imageFile, x => x.CameraGain,
            "GAIN", "CCD-GAIN", "EGAIN");
        ExtractKeyword<int, int?>(header, imageFile, x => x.CameraOffset,
            "OFFSET");
        ExtractKeyword<int, int?>(header, imageFile, x => x.Binning,
            "XBINNING", "CCDXBIN");
        ExtractKeyword<double, double?>(header, imageFile, x => x.PixelSize,
            "XPIXSZ");
        ExtractKeyword<string, string?>(header, imageFile, x => x.ReadoutMode!,
            "READOUTM");
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
        ExtractKeyword<int, int?>(header, imageFile, x => x.RotatorPosition,
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
        ExtractKeyword<string, string?>(header, imageFile, x => x.MountName!,
            "TELESCOP");
        ExtractKeyword<double, double?>(header, imageFile, x => x.RightAscension,
            "RA");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Declination,
            "DEC");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Altitude,
            "CENTALT");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Azimuth,
            "CENTAZ");
    }

    private void ExtractTelescopeKeywords(ImageHeader header, ImageFile imageFile)
    {
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
        ExtractKeyword<string, string?>(header, imageFile, x => x.ObjectName!,
            "OBJECT");
    }

    private void ExtractSiteKeywords(ImageHeader header, ImageFile imageFile)
    {
        ExtractKeyword<double, double?>(header, imageFile, x => x.Latitude,
            "SITELAT");
        ExtractKeyword<double, double?>(header, imageFile, x => x.Longitude,
            "SITELONG", "SITELON");
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
