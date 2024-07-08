using AutoMapper;
using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class ImageFileViewModel : ObservableObject, IEquatable<ImageFileViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] LibraryViewModel _library = null!;
    [ObservableProperty] AvaloniaList<TagViewModel> _tags = new();
    [ObservableProperty] string _path = null!;
    [ObservableProperty] ImageType _type;
    [ObservableProperty] ImageKind _kind;
    [ObservableProperty] string? _objectName;
    [ObservableProperty] double? _exposure;
    [ObservableProperty] string? _filterName;
    [ObservableProperty] int? _binning;
    [ObservableProperty] DateTime? _observationTimestampUtc;

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

public class ImageFileProfile : Profile
{
    public ImageFileProfile()
    {
        CreateMap<ImageFile, ImageFileViewModel>();
    }
}
