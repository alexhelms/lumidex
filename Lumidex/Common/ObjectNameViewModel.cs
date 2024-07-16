namespace Lumidex.Common;

public partial class ObjectNameViewModel : ObservableObject, IEquatable<ObjectNameViewModel?>
{
    [ObservableProperty] string _objectName = string.Empty;
    [ObservableProperty] ObservableCollectionEx<AliasViewModel> _aliases = new();
    [ObservableProperty] string? _newAliasName;

    #region Equality

    public override bool Equals(object? obj)
    {
        return Equals(obj as ObjectNameViewModel);
    }

    public bool Equals(ObjectNameViewModel? other)
    {
        return other is not null &&
               ObjectName.Equals(other.ObjectName, StringComparison.InvariantCultureIgnoreCase);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ObjectName);
    }

    public static bool operator ==(ObjectNameViewModel? left, ObjectNameViewModel? right)
    {
        return EqualityComparer<ObjectNameViewModel>.Default.Equals(left, right);
    }

    public static bool operator !=(ObjectNameViewModel? left, ObjectNameViewModel? right)
    {
        return !(left == right);
    }

    #endregion
}
