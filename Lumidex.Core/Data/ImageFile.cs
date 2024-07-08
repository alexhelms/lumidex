using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lumidex.Core.Data;

[Index(nameof(HeaderHash))]
[Index(nameof(Path))]
public class ImageFile
{
    public int Id { get; set; }

    public int LibraryId { get; set; }

    public Library Library { get; set; } = null!;

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();

    public ICollection<AssociatedName> AssociatedNames { get; set; } = new List<AssociatedName>();

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string HeaderHash { get; set; } = null!;

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string Path { get; set; } = null!;

    [Column(TypeName = "DATETIME")]
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "DATETIME")]
    public DateTime? UpdatedOn { get; set; }

    public ImageType Type { get; set; }
    
    public ImageKind Kind { get; set; }

    #region Camera

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? CameraName { get; set; }

    public double? Exposure { get; set; }

    public double? CameraTemperatureSetPoint { get; set; }

    public double? CameraTemperature { get; set; }

    public int? CameraGain { get; set; }

    public int? CameraOffset { get; set; }

    public int? Binning { get; set; }

    public double? PixelSize { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? ReadoutMode { get; set; }

    #endregion

    #region Focuser 

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? FocuserName { get; set; }

    public int? FocuserPosition { get; set; }

    public double? FocuserTemperature { get; set; }

    #endregion

    #region Rotator

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? RotatorName { get; set; }

    public double? RotatorPosition { get; set; }

    #endregion

    #region Filter Wheel

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? FilterWheelName { get; set; }


    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? FilterName { get; set; }

    #endregion

    #region Mount

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? MountName { get; set; }

    public double? RightAscension { get; set; }

    public double? Declination { get; set; }

    public double? Altitude { get; set; }

    public double? Azimuth { get; set; }

    #endregion

    #region Telescope

    public double? FocalLength { get; set; }

    public double? Airmass { get; set; }

    #endregion

    #region Target

    [Column(TypeName = "DATETIME")]
    public DateTime? ObservationTimestampUtc { get; set; }

    [Column(TypeName = "TEXT COLLATE NOCASE")]
    public string? ObjectName { get; set; }

    #endregion

    #region Site

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? Elevation { get; set; }

    #endregion

    #region Weather

    public double? DewPoint { get; set; }

    public double? Humidity { get; set; }

    public double? Pressure { get; set; }

    public double? Temperature { get; set; }

    #endregion
}
