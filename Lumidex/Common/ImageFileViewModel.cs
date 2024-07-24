using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class ImageFileViewModel : ObservableObject, IEquatable<ImageFileViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] string _libraryName = string.Empty;
    [ObservableProperty] ObservableCollectionEx<TagViewModel> _tags = new();
    [ObservableProperty] string _path = null!;
    [ObservableProperty] long _fileSize;

    [ObservableProperty]
    [property: UserEditable]
    ImageType _type;

    [ObservableProperty]
    [property: UserEditable]
    ImageKind _kind;

    #region Camera

    [ObservableProperty]
    [property: UserEditable]
    string? _cameraName;

    [ObservableProperty]
    [property: UserEditable]
    double? _exposure;

    [ObservableProperty]
    [property: UserEditable]
    double? _cameraTemperatureSetPoint;

    [ObservableProperty]
    [property: UserEditable]
    double? _cameraTemperature;

    [ObservableProperty]
    [property: UserEditable]
    int? _cameraGain;

    [ObservableProperty]
    [property: UserEditable]
    int? _cameraOffset;

    [ObservableProperty]
    [property: UserEditable]
    int? _binning;

    [ObservableProperty]
    [property: UserEditable]
    double? _pixelSize;

    [ObservableProperty]
    [property: UserEditable]
    string? _readoutMode;

    #endregion

    #region Focuser

    [ObservableProperty]
    [property: UserEditable]
    string? _focuserName;

    [ObservableProperty]
    [property: UserEditable]
    int? _focuserPosition;

    [ObservableProperty]
    [property: UserEditable]
    double? _focuserTemperature;

    #endregion

    #region Rotator

    [ObservableProperty]
    [property: UserEditable]
    string? _rotatorName;

    [ObservableProperty]
    [property: UserEditable]
    double? _rotatorPosition;

    #endregion

    #region Filter Wheel

    [ObservableProperty]
    [property: UserEditable]
    string? _filterWheelName;

    [ObservableProperty]
    [property: UserEditable]
    string? _filterName;

    #endregion

    #region Mount

    [ObservableProperty]
    [property: UserEditable]
    string? _mountName;

    [ObservableProperty]
    [property: UserEditable]
    double? _rightAscension;

    [ObservableProperty]
    [property: UserEditable]
    double? _declination;

    [ObservableProperty]
    [property: UserEditable]
    double? _altitude;

    [ObservableProperty]
    [property: UserEditable]
    double? _azimuth;

    #endregion

    #region Telescope

    [ObservableProperty]
    [property: UserEditable]
    double? _focalLength;

    [ObservableProperty]
    [property: UserEditable]
    double? _airmass;

    #endregion

    #region Target

    [ObservableProperty]
    [property: UserEditable]
    DateTime? _observationTimestampUtc;

    [ObservableProperty]
    [property: UserEditable]
    DateTime? _observationTimestampLocal;

    [ObservableProperty]
    [property: UserEditable]
    string? _objectName;

    #endregion

    #region Site

    [ObservableProperty]
    [property: UserEditable]
    double? _latitude;

    [ObservableProperty]
    [property: UserEditable]
    double? _longitude;

    [ObservableProperty]
    [property: UserEditable]
    double? _elevation;

    #endregion

    #region Weather

    [ObservableProperty]
    [property: UserEditable]
    double? _dewPoint;

    [ObservableProperty]
    [property: UserEditable]
    double? _humidity;

    [ObservableProperty]
    [property: UserEditable]
    double? _pressure;

    [ObservableProperty]
    [property: UserEditable]
    double? _temperature;

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
