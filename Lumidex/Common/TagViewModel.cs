using AutoMapper;
using Avalonia.Media;
using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class TagViewModel : ObservableObject, IEquatable<TagViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] string _name = string.Empty;
    [ObservableProperty] Color _color = Colors.Gray;
    [ObservableProperty] AvaloniaList<ImageFileViewModel> _taggedImages = new();
    [ObservableProperty] bool _isSelected;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as TagViewModel);
    }

    public bool Equals(TagViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(TagViewModel? left, TagViewModel? right)
    {
        return EqualityComparer<TagViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(TagViewModel? left, TagViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}

public class TagProfile : Profile
{
    public TagProfile()
    {
        CreateMap<Tag, TagViewModel>()
            .ForMember(vm => vm.Color, opt => opt.MapFrom(src => Color.Parse(src.Color)));
    }
}