using Lumidex.Core.Data;

namespace Lumidex.Common;

public partial class AlternateNameViewModel : ObservableObject, IEquatable<AlternateNameViewModel?>
{
    [ObservableProperty] int _id;
    [ObservableProperty] string _name = string.Empty;

    public override string ToString()
    {
        return Name;
    }

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as AlternateNameViewModel);
    }

    public bool Equals(AlternateNameViewModel? other)
    {
        return other is not null &&
               Id == other.Id;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool operator ==(AlternateNameViewModel? left, AlternateNameViewModel? right)
    {
        return EqualityComparer<AlternateNameViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(AlternateNameViewModel? left, AlternateNameViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}

public static class AlternateNameMapper
{
    public static AlternateNameViewModel ToViewModel(AlternateName alternateName)
    {
        var alternateNameViewModel = new AlternateNameViewModel
        {
            Id = alternateName.Id,
            Name = alternateName.Name,
        };

        return alternateNameViewModel;
    }
}