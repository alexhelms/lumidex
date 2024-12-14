using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class ImageFileViewModel : ObservableObject, IEquatable<ImageFileViewModel?>
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string LibraryName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollectionEx<TagViewModel> Tags { get; set; } = new();

    [ObservableProperty]
    public partial string Path { get; set; } = null!;

    [ObservableProperty]
    public partial long FileSize { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial ImageType Type { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial ImageKind Kind { get; set; }

    #region Camera

    [ObservableProperty]
    [UserEditable]
    public partial string? CameraName { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Exposure { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? CameraTemperatureSetPoint { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? CameraTemperature { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial int? CameraGain { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial int? CameraOffset { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial int? Binning { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? PixelSize { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial string? ReadoutMode { get; set; }

    #endregion

    #region Focuser

    [ObservableProperty]
    [UserEditable]
    public partial string? FocuserName { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial int? FocuserPosition { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? FocuserTemperature { get; set; }

    #endregion

    #region Rotator

    [ObservableProperty]
    [UserEditable]
    public partial string? RotatorName { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? RotatorPosition { get; set; }

    #endregion

    #region Filter Wheel

    [ObservableProperty]
    [UserEditable]
    public partial string? FilterWheelName { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial string? FilterName { get; set; }

    #endregion

    #region Mount

    [ObservableProperty]
    [UserEditable]
    public partial double? RightAscension { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Declination { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Altitude { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Azimuth { get; set; }

    #endregion

    #region Telescope

    [ObservableProperty]
    [UserEditable]
    public partial string? TelescopeName { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? FocalLength { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Airmass { get; set; }

    #endregion

    #region Target

    [ObservableProperty]
    [UserEditable]
    public partial DateTime? ObservationTimestampUtc { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial DateTime? ObservationTimestampLocal { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial string? ObjectName { get; set; }

    #endregion

    #region Site

    [ObservableProperty]
    [UserEditable]
    public partial double? Latitude { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Longitude { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Elevation { get; set; }

    #endregion

    #region Weather

    [ObservableProperty]
    [UserEditable]
    public partial double? DewPoint { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Humidity { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Pressure { get; set; }

    [ObservableProperty]
    [UserEditable]
    public partial double? Temperature { get; set; }

    #endregion

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as ImageFileViewModel);
    }

    public bool Equals(ImageFileViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(ImageFileViewModel? left, ImageFileViewModel? right)
    {
        return EqualityComparer<ImageFileViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(ImageFileViewModel? left, ImageFileViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}
